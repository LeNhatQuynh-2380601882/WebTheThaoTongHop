using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CustomersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public CustomersController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /admin/customers
        public async Task<IActionResult> Index(string? q, bool? isActive, int page = 1)
        {
            ViewData["Title"] = "Quản lý khách hàng";
            const int pageSize = 20;

            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.Email!.Contains(q) || (u.FullName != null && u.FullName.Contains(q)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            int total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get order counts per user
            var userIds = users.Select(u => u.Id).ToList();
            var orderCounts = await _db.Orders
                .Where(o => userIds.Contains(o.UserId))
                .GroupBy(o => o.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), Total = g.Sum(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0 })
                .ToDictionaryAsync(x => x.UserId, x => new { x.Count, x.Total });

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchQuery = q;
            ViewBag.IsActive = isActive;
            ViewBag.OrderCounts = orderCounts;

            return View(users);
        }

        // GET /admin/customers/detail/{id}
        public async Task<IActionResult> Detail(string id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var roles = await _userManager.GetRolesAsync(user);

            ViewData["Title"] = $"Khách hàng – {user.FullName ?? user.Email}";
            ViewBag.Orders = orders;
            ViewBag.Roles = roles;
            ViewBag.TotalSpent = orders
                .Where(o => o.Status == OrderStatus.DaGiao)
                .Sum(o => o.TotalAmount + o.ShippingFee - o.DiscountAmount);

            return View(user);
        }

        // POST /admin/customers/toggle/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(string id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _db.SaveChangesAsync();

            TempData["Success"] = user.IsActive ? "✅ Đã kích hoạt tài khoản." : "⚠️ Đã khóa tài khoản.";
            return RedirectToAction("Index");
        }

        // POST /admin/customers/setrole
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetRole(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = $"✅ Đã đặt vai trò '{role}' cho {user.Email}.";
            return RedirectToAction("Detail", new { id = userId });
        }
    }
}
