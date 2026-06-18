using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;
using TamThaiTuSport.Models.ViewModels;

namespace TamThaiTuSport.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;
        private readonly Services.MoMoService _momoService;

        public CartController(AppDbContext db, UserManager<AppUser> userManager, Services.MoMoService momoService)
        {
            _db = db;
            _userManager = userManager;
            _momoService = momoService;
        }

        // GET /gio-hang
        [Route("gio-hang")]
        public IActionResult Index()
        {
            ViewData["Title"] = "Giỏ hàng";
            return View();
        }

        // GET /thanh-toan
        [Route("thanh-toan")]
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            ViewData["Title"] = "Thanh toán";
            var user = await _userManager.GetUserAsync(User);
            var vm = new CheckoutViewModel
            {
                CustomerName = user?.FullName ?? "",
                CustomerPhone = user?.PhoneNumber ?? "",
                CustomerEmail = user?.Email ?? "",
            };
            return View(vm);
        }

        // POST /thanh-toan
        [HttpPost, Route("thanh-toan"), Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder([FromForm] CheckoutViewModel vm, [FromForm] string cartJson)
        {
            ViewData["Title"] = "Thanh toán";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Parse cart from JSON (sent by client)
            List<CartItemViewModel> cartItems;
            try
            {
                cartItems = System.Text.Json.JsonSerializer.Deserialize<List<CartItemViewModel>>(cartJson ?? "[]",
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch { cartItems = new(); }

            if (!cartItems.Any())
            {
                ModelState.AddModelError("", "Giỏ hàng trống. Vui lòng thêm sản phẩm.");
                return View("Checkout", vm);
            }

            // Validate coupon
            decimal discount = 0;
            Coupon? coupon = null;
            if (!string.IsNullOrWhiteSpace(vm.CouponCode))
            {
                coupon = await _db.Coupons.FirstOrDefaultAsync(c =>
                    c.Code == vm.CouponCode.Trim().ToUpper() &&
                    c.IsActive &&
                    (c.MaxUsage == null || c.UsedCount < c.MaxUsage) &&
                    (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow));

                if (coupon == null)
                {
                    ModelState.AddModelError("", "Mã giảm giá không hợp lệ hoặc đã hết hạn.");
                    return View("Checkout", vm);
                }
            }

            // Calculate totals
            decimal subtotal = cartItems.Sum(i => i.UnitPrice * i.Quantity);

            if (coupon != null)
            {
                discount = coupon.IsPercent
                    ? Math.Round(subtotal * coupon.DiscountValue / 100m, 0)
                    : coupon.DiscountValue;

                if (coupon.MaxDiscount.HasValue && discount > coupon.MaxDiscount.Value)
                    discount = coupon.MaxDiscount.Value;
            }

            decimal shippingFee = subtotal >= 500000 ? 0 : 30000;
            decimal totalAmount = subtotal + shippingFee - discount;

            // Determine payment method enum
            PaymentMethod paymentMethod = vm.PaymentMethod switch
            {
                "MoMo" => PaymentMethod.MoMo,
                "ZaloPay" => PaymentMethod.ZaloPay,
                "VNPay" => PaymentMethod.VNPay,
                "ChuyenKhoan" => PaymentMethod.ChuyenKhoan,
                _ => PaymentMethod.COD
            };

            // Create order (wrapped in an execution strategy transaction to support EF Core retries)
            IActionResult result = null!;
            try
            {
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _db.Database.BeginTransactionAsync();
                    try
                    {
                        var order = new Order
                        {
                            UserId = user.Id,
                            CustomerName = vm.CustomerName.Trim(),
                            CustomerPhone = vm.CustomerPhone?.Trim(),
                            CustomerEmail = vm.CustomerEmail?.Trim() ?? user.Email ?? "",
                            ShippingAddress = $"{vm.ShippingAddress}, {vm.Ward}, {vm.District}, {vm.Province}",
                            PaymentMethod = paymentMethod,
                            Note = vm.Note?.Trim(),
                            TotalAmount = subtotal,
                            ShippingFee = shippingFee,
                            DiscountAmount = discount,
                            CouponId = coupon?.Id,
                            Status = OrderStatus.ChoPhanHoi,
                            PaymentStatus = paymentMethod == PaymentMethod.COD ? PaymentStatus.ChuaThanhToan : PaymentStatus.ChuaThanhToan,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _db.Orders.Add(order);
                        await _db.SaveChangesAsync();

                        // Add order details & update stock
                        foreach (var item in cartItems)
                        {
                            var product = await _db.Products.FindAsync(item.ProductId);
                            if (product == null) continue;

                            _db.OrderDetails.Add(new OrderDetail
                            {
                                OrderId = order.Id,
                                ProductId = item.ProductId,
                                UnitPrice = item.UnitPrice,
                                Quantity = item.Quantity,
                                Size = item.Size,
                                Color = item.Color
                            });

                            product.Stock = Math.Max(0, product.Stock - item.Quantity);
                        }

                        // Increment coupon usage
                        if (coupon != null)
                        {
                            coupon.UsedCount++;
                        }

                        // Add loyalty points (1 point per 10,000₫)
                        int pointsEarned = (int)(totalAmount / 10000);
                        if (pointsEarned > 0)
                        {
                            user.LoyaltyPointsTotal += pointsEarned;
                            _db.LoyaltyPoints.Add(new LoyaltyPoint
                            {
                                UserId = user.Id,
                                Points = pointsEarned,
                                Description = $"Đơn hàng #{order.Id}",
                                OrderId = order.Id,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        await _db.SaveChangesAsync();

                        TempData["OrderId"] = order.Id;
                        TempData["OrderTotal"] = totalAmount.ToString("N0");
                        TempData["PointsEarned"] = pointsEarned;

                        if (paymentMethod == PaymentMethod.MoMo)
                        {
                            // Build dynamic callback URLs matching current request domain/port
                            var request = HttpContext.Request;
                            var baseUri = $"{request.Scheme}://{request.Host}";
                            var returnUrl = $"{baseUri}/Payment/MoMoReturn";
                            var notifyUrl = $"{baseUri}/Payment/MoMoIpn";

                            var orderInfo = $"Thanh toán đơn hàng #{order.Id} tại Tam Thái Tử Sport";
                            var payUrl = await _momoService.CreatePaymentAsync(order.Id, (long)totalAmount, orderInfo, returnUrl, notifyUrl);
                            if (!string.IsNullOrEmpty(payUrl) && !payUrl.StartsWith("ERROR:"))
                            {
                                await transaction.CommitAsync();
                                result = Redirect(payUrl); // Chuyển sang trang thanh toán MoMo
                                return;
                            }

                            // If MoMo initialization failed, throw to trigger rollback
                            var errorDetails = (payUrl != null && payUrl.StartsWith("ERROR:")) ? payUrl.Substring(6) : "Không có phản hồi từ cổng thanh toán";
                            throw new InvalidOperationException($"Khởi tạo thanh toán MoMo thất bại. Chi tiết: {errorDetails}");
                        }

                        await transaction.CommitAsync();
                        result = RedirectToAction("OrderSuccess");
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw; // Let it bubble up to strategy
                    }
                });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Đã xảy ra lỗi khi xử lý đơn hàng: {ex.Message}");
                return View("Checkout", vm);
            }

            return result;
        }

        // GET /dat-hang-thanh-cong
        [Route("dat-hang-thanh-cong")]
        [Authorize]
        public IActionResult OrderSuccess()
        {
            ViewData["Title"] = "Đặt hàng thành công";

            if (TempData["OrderId"] == null)
                return RedirectToAction("Index", "Home");

            ViewBag.OrderId = TempData["OrderId"];
            ViewBag.OrderTotal = TempData["OrderTotal"];
            ViewBag.PointsEarned = TempData["PointsEarned"];
            return View();
        }

        // POST /api/cart/validate-coupon
        [HttpPost("/api/cart/validate-coupon")]
        [Authorize]
        public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Code))
                return Ok(new { success = false, message = "Vui lòng nhập mã giảm giá." });

            var coupon = await _db.Coupons.FirstOrDefaultAsync(c =>
                c.Code == req.Code.Trim().ToUpper() &&
                c.IsActive &&
                (c.MaxUsage == null || c.UsedCount < c.MaxUsage) &&
                (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow));

            if (coupon == null)
                return Ok(new { success = false, message = "Mã giảm giá không hợp lệ hoặc đã hết hạn." });

            decimal discount = coupon.IsPercent
                ? Math.Round(req.Subtotal * coupon.DiscountValue / 100m, 0)
                : coupon.DiscountValue;

            if (coupon.MaxDiscount.HasValue && discount > coupon.MaxDiscount.Value)
                discount = coupon.MaxDiscount.Value;

            return Ok(new
            {
                success = true,
                message = $"✅ Áp dụng mã thành công! Giảm {discount:N0}₫",
                discount,
                discountType = coupon.IsPercent ? "percent" : "fixed",
                discountValue = coupon.DiscountValue,
                description = coupon.Description
            });
        }

        public class ValidateCouponRequest
        {
            public string Code { get; set; } = "";
            public decimal Subtotal { get; set; }
        }
    }
}
