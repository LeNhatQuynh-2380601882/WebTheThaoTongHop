using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class OrdersController : Controller
    {
        private readonly AppDbContext _db;

        public OrdersController(AppDbContext db)
        {
            _db = db;
        }

        // GET /admin/orders
        public async Task<IActionResult> Index(string? status = null, int page = 1)
        {
            ViewData["Title"] = "Quản lý đơn hàng";
            const int pageSize = 20;

            var query = _db.Orders
                .Include(o => o.OrderDetails)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var s))
                query = query.Where(o => o.Status == s);

            int total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentStatus = status;
            ViewBag.Counts = new
            {
                All = await _db.Orders.CountAsync(),
                ChoPhanHoi = await _db.Orders.CountAsync(o => o.Status == OrderStatus.ChoPhanHoi),
                DaXacNhan = await _db.Orders.CountAsync(o => o.Status == OrderStatus.DaXacNhan),
                DangGiao = await _db.Orders.CountAsync(o => o.Status == OrderStatus.DangGiao),
                DaGiao = await _db.Orders.CountAsync(o => o.Status == OrderStatus.DaGiao),
                DaHuy = await _db.Orders.CountAsync(o => o.Status == OrderStatus.DaHuy),
            };

            return View(orders);
        }

        // GET /admin/orders/detail/{id}
        public async Task<IActionResult> Detail(int id)
        {
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .Include(o => o.User)
                .Include(o => o.Coupon)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();
            ViewData["Title"] = $"Đơn hàng #{id}";
            return View(order);
        }

        // POST /admin/orders/update-status
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? trackingCode)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(trackingCode))
                order.TrackingCode = trackingCode;

            await _db.SaveChangesAsync();
            TempData["Success"] = "✅ Cập nhật trạng thái đơn hàng thành công.";
            return RedirectToAction("Detail", new { id });
        }
    }
}
