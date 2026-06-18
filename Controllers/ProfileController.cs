using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public ProfileController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /tai-khoan
        [Route("tai-khoan")]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Tài khoản của tôi";
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        // GET /tai-khoan/don-hang
        [Route("tai-khoan/don-hang")]
        public async Task<IActionResult> Orders(string? status = null, int page = 1)
        {
            ViewData["Title"] = "Đơn hàng của tôi";
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var query = _db.Orders
                .Where(o => o.UserId == user.Id)
                .Include(o => o.OrderDetails)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, out var s))
                query = query.Where(o => o.Status == s);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * 10)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalOrders = await _db.Orders.CountAsync(o => o.UserId == user.Id);
            ViewBag.CurrentStatus = status;
            ViewBag.Page = page;
            return View(orders);
        }

        // GET /tai-khoan/don-hang/{id}
        [Route("tai-khoan/don-hang/{id}")]
        public async Task<IActionResult> OrderDetail(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();
            ViewData["Title"] = $"Đơn hàng #{id}";
            return View(order);
        }

        // POST /tai-khoan/don-hang/{id}/huy
        [HttpPost, Route("tai-khoan/don-hang/{id}/huy"), ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _db.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user!.Id);

            if (order == null) return NotFound();

            if (order.Status != OrderStatus.ChoPhanHoi)
            {
                TempData["Error"] = "Chỉ có thể hủy đơn hàng khi đang ở trạng thái Chờ duyệt.";
                return Redirect($"/tai-khoan/don-hang/{id}");
            }

            // Restore stock
            foreach (var detail in order.OrderDetails)
            {
                var product = await _db.Products.FindAsync(detail.ProductId);
                if (product != null) product.Stock += detail.Quantity;
            }

            order.Status = OrderStatus.DaHuy;
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Đơn hàng đã được hủy thành công.";
            return RedirectToAction("Orders");
        }

        // POST /tai-khoan/cap-nhat
        [HttpPost, Route("tai-khoan/cap-nhat"), ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string fullName, string? phoneNumber, DateTime? dateOfBirth)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            user.FullName = fullName.Trim();
            user.PhoneNumber = phoneNumber?.Trim();
            user.DateOfBirth = dateOfBirth;

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "✅ Cập nhật thông tin thành công.";
            return RedirectToAction("Index");
        }

        // GET /api/profile/stats
        [HttpGet("/api/profile/stats")]
        public async Task<IActionResult> Stats()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var totalOrders = await _db.Orders.CountAsync(o => o.UserId == user.Id);
            var totalSpent = await _db.Orders
                .Where(o => o.UserId == user.Id && o.Status == OrderStatus.DaGiao)
                .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0;

            return Ok(new { totalOrders, totalSpent, loyaltyPoints = user.LoyaltyPointsTotal });
        }
    }
}
