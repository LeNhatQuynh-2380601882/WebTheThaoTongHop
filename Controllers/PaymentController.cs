using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;
using TamThaiTuSport.Services;
using Microsoft.Extensions.Logging;

namespace TamThaiTuSport.Controllers
{
    public class PaymentController : Controller
    {
        private readonly MoMoService _momoService;
        private readonly AppDbContext _db;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(MoMoService momoService, AppDbContext db, ILogger<PaymentController> logger)
        {
            _momoService = momoService;
            _db = db;
            _logger = logger;
        }

        // GET: /Payment/MoMoReturn (Khách hàng quay lại sau khi thanh toán)
        public async Task<IActionResult> MoMoReturn()
        {
            var queryParams = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
            var signature = queryParams.GetValueOrDefault("signature", "");

            if (!_momoService.VerifySignature(queryParams, signature))
            {
                _logger.LogWarning("Chữ ký MoMo trả về không hợp lệ!");
                TempData["Error"] = "Thanh toán không hợp lệ hoặc bị giả mạo!";
                return RedirectToAction("Index", "Home");
            }

            var resultCode = queryParams.GetValueOrDefault("resultCode", "");
            var momoOrderId = queryParams.GetValueOrDefault("orderId", "");
            var transId = queryParams.GetValueOrDefault("transId", "");

            // Định dạng momoOrderId gửi đi: ORDER_{orderId}_{Ticks}
            var orderIdStr = momoOrderId.Split('_').ElementAtOrDefault(1);
            if (!int.TryParse(orderIdStr, out int orderId))
            {
                return RedirectToAction("Index", "Cart");
            }

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng");
            }

            var totalAmount = order.TotalAmount + order.ShippingFee - order.DiscountAmount;

            if (resultCode == "0") // 0 tức là thành công
            {
                if (order.PaymentStatus != PaymentStatus.DaThanhToan)
                {
                    order.PaymentStatus = PaymentStatus.DaThanhToan;
                    order.Status = OrderStatus.ChoPhanHoi; // Chờ duyệt
                    order.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }

                TempData["OrderId"] = order.Id;
                TempData["OrderTotal"] = totalAmount.ToString("N0");
                TempData["PointsEarned"] = (int)(totalAmount / 10000);

                TempData["Success"] = $"Thanh toán MoMo thành công! Mã GD: {transId}";
                return RedirectToAction("OrderSuccess", "Cart");
            }
            else
            {
                // Thanh toán thất bại hoặc người dùng hủy bỏ -> Hủy đơn hàng và phục hồi tồn kho
                if (order.Status != OrderStatus.DaHuy)
                {
                    order.Status = OrderStatus.DaHuy;
                    order.UpdatedAt = DateTime.UtcNow;

                    // Phục hồi stock
                    var details = await _db.OrderDetails.Where(d => d.OrderId == order.Id).ToListAsync();
                    foreach (var detail in details)
                    {
                        var product = await _db.Products.FindAsync(detail.ProductId);
                        if (product != null) product.Stock += detail.Quantity;
                    }

                    await _db.SaveChangesAsync();
                }

                TempData["Error"] = $"Thanh toán thất bại hoặc đã bị hủy. Mã lỗi: {resultCode}";
                return RedirectToAction("Index", "Cart");
            }
        }

        // POST: /Payment/MoMoIpn (Webhook nhận thông báo ngầm từ MoMo Server)
        [HttpPost]
        public async Task<IActionResult> MoMoIpn([FromBody] Dictionary<string, string> ipnData)
        {
            if (ipnData == null) return BadRequest("No data");

            var signature = ipnData.GetValueOrDefault("signature", "");
            if (!_momoService.VerifySignature(ipnData, signature))
            {
                _logger.LogWarning("MoMo IPN Signature không hợp lệ!");
                return BadRequest("Invalid Signature");
            }

            var resultCode = ipnData.GetValueOrDefault("resultCode", "");
            var momoOrderId = ipnData.GetValueOrDefault("orderId", "");
            var transId = ipnData.GetValueOrDefault("transId", "");

            var orderIdStr = momoOrderId.Split('_').ElementAtOrDefault(1);
            if (int.TryParse(orderIdStr, out int orderId))
            {
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    if (resultCode == "0")
                    {
                        if (order.PaymentStatus != PaymentStatus.DaThanhToan)
                        {
                            order.PaymentStatus = PaymentStatus.DaThanhToan;
                            order.Status = OrderStatus.ChoPhanHoi;
                            order.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync();
                        }
                        _logger.LogInformation($"Đơn hàng #{orderId} đã được thanh toán qua MoMo IPN.");
                    }
                    else
                    {
                        if (order.Status != OrderStatus.DaHuy)
                        {
                            order.Status = OrderStatus.DaHuy;
                            order.UpdatedAt = DateTime.UtcNow;

                            var details = await _db.OrderDetails.Where(d => d.OrderId == order.Id).ToListAsync();
                            foreach (var detail in details)
                            {
                                var product = await _db.Products.FindAsync(detail.ProductId);
                                if (product != null) product.Stock += detail.Quantity;
                            }
                            await _db.SaveChangesAsync();
                        }
                        _logger.LogInformation($"Đơn hàng #{orderId} bị hủy do thanh toán thất bại qua MoMo IPN.");
                    }
                }
            }

            return Ok();
        }
    }
}
