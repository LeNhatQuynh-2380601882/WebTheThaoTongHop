using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class CouponsController : Controller
    {
        private readonly AppDbContext _db;

        public CouponsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /admin/coupons
        public async Task<IActionResult> Index(string? q, bool? isActive, int page = 1)
        {
            ViewData["Title"] = "Quản lý mã giảm giá";
            const int pageSize = 20;

            var query = _db.Coupons.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.Code.Contains(q) || (c.Description != null && c.Description.Contains(q)));

            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            int total = await query.CountAsync();
            var coupons = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchQuery = q;
            ViewBag.IsActive = isActive;

            return View(coupons);
        }

        // GET /admin/coupons/create
        public IActionResult Create()
        {
            ViewData["Title"] = "Thêm mã giảm giá";
            return View(new Coupon());
        }

        // POST /admin/coupons/create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Coupon coupon)
        {
            // Check unique code
            if (await _db.Coupons.AnyAsync(c => c.Code == coupon.Code.ToUpper()))
            {
                TempData["Error"] = "❌ Mã giảm giá đã tồn tại.";
                return View(coupon);
            }

            coupon.Code = coupon.Code.ToUpper().Trim();
            coupon.CreatedAt = DateTime.UtcNow;
            coupon.UsedCount = 0;

            _db.Coupons.Add(coupon);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Thêm mã giảm giá thành công.";
            return RedirectToAction("Index");
        }

        // GET /admin/coupons/edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var coupon = await _db.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            ViewData["Title"] = $"Sửa mã – {coupon.Code}";
            return View(coupon);
        }

        // POST /admin/coupons/edit/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Coupon coupon)
        {
            var existing = await _db.Coupons.FindAsync(id);
            if (existing == null) return NotFound();

            // Check unique code (exclude self)
            if (await _db.Coupons.AnyAsync(c => c.Code == coupon.Code.ToUpper() && c.Id != id))
            {
                TempData["Error"] = "❌ Mã giảm giá đã tồn tại.";
                return View(coupon);
            }

            existing.Code = coupon.Code.ToUpper().Trim();
            existing.Description = coupon.Description;
            existing.IsPercent = coupon.IsPercent;
            existing.DiscountValue = coupon.DiscountValue;
            existing.MinOrderAmount = coupon.MinOrderAmount;
            existing.MaxDiscount = coupon.MaxDiscount;
            existing.MaxUsage = coupon.MaxUsage;
            existing.ExpiresAt = coupon.ExpiresAt;
            existing.IsActive = coupon.IsActive;

            await _db.SaveChangesAsync();
            TempData["Success"] = "✅ Cập nhật mã giảm giá thành công.";
            return RedirectToAction("Index");
        }

        // POST /admin/coupons/toggle/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var coupon = await _db.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            coupon.IsActive = !coupon.IsActive;
            await _db.SaveChangesAsync();

            TempData["Success"] = coupon.IsActive ? "✅ Đã kích hoạt mã giảm giá." : "⚠️ Đã vô hiệu hóa mã giảm giá.";
            return RedirectToAction("Index");
        }

        // POST /admin/coupons/delete/{id}
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var coupon = await _db.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            // Check if coupon is used in orders
            var used = await _db.Orders.AnyAsync(o => o.CouponId == id);
            if (used)
            {
                TempData["Error"] = "❌ Không thể xóa mã đã được sử dụng. Hãy vô hiệu hóa thay thế.";
                return RedirectToAction("Index");
            }

            _db.Coupons.Remove(coupon);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Đã xóa mã giảm giá.";
            return RedirectToAction("Index");
        }
    }
}
