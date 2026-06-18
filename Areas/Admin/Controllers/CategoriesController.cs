using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class CategoriesController : Controller
    {
        private readonly AppDbContext _db;

        public CategoriesController(AppDbContext db)
        {
            _db = db;
        }

        // GET /admin/categories
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Quản lý danh mục";
            var categories = await _db.Categories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            // Attach product counts
            var productCounts = await _db.Products
                .Where(p => p.IsActive)
                .GroupBy(p => p.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CategoryId, x => x.Count);

            ViewBag.ProductCounts = productCounts;
            return View(categories);
        }

        // GET /admin/categories/create
        public IActionResult Create()
        {
            ViewData["Title"] = "Thêm danh mục";
            return View(new Category());
        }

        // POST /admin/categories/create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (string.IsNullOrEmpty(category.Slug))
                category.Slug = GenerateSlug(category.Name);

            category.CreatedAt = DateTime.UtcNow;
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Thêm danh mục thành công.";
            return RedirectToAction("Index");
        }

        // GET /admin/categories/edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();
            ViewData["Title"] = "Chỉnh sửa danh mục";
            return View(category);
        }

        // POST /admin/categories/edit/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            var existing = await _db.Categories.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = category.Name;
            existing.Slug = string.IsNullOrEmpty(category.Slug) ? GenerateSlug(category.Name) : category.Slug;
            existing.IconClass = category.IconClass;
            existing.Color = category.Color;
            existing.ImageUrl = category.ImageUrl;
            existing.SortOrder = category.SortOrder;
            existing.IsActive = category.IsActive;

            await _db.SaveChangesAsync();
            TempData["Success"] = "✅ Cập nhật danh mục thành công.";
            return RedirectToAction("Index");
        }

        // POST /admin/categories/toggle/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var category = await _db.Categories.FindAsync(id);
            if (category == null) return NotFound();

            category.IsActive = !category.IsActive;
            await _db.SaveChangesAsync();

            TempData["Success"] = category.IsActive ? "✅ Đã kích hoạt danh mục." : "⚠️ Đã ẩn danh mục.";
            return RedirectToAction("Index");
        }

        private static string GenerateSlug(string name)
        {
            var slug = name.ToLowerInvariant();
            var map = new Dictionary<string, string> {
                {"à","a"},{"á","a"},{"ả","a"},{"ã","a"},{"ạ","a"},
                {"ă","a"},{"ắ","a"},{"ặ","a"},{"ằ","a"},{"ẳ","a"},{"ẵ","a"},
                {"â","a"},{"ấ","a"},{"ầ","a"},{"ẩ","a"},{"ẫ","a"},{"ậ","a"},
                {"è","e"},{"é","e"},{"ẻ","e"},{"ẽ","e"},{"ẹ","e"},
                {"ê","e"},{"ế","e"},{"ề","e"},{"ể","e"},{"ễ","e"},{"ệ","e"},
                {"ì","i"},{"í","i"},{"ỉ","i"},{"ĩ","i"},{"ị","i"},
                {"ò","o"},{"ó","o"},{"ỏ","o"},{"õ","o"},{"ọ","o"},
                {"ô","o"},{"ố","o"},{"ồ","o"},{"ổ","o"},{"ỗ","o"},{"ộ","o"},
                {"ơ","o"},{"ớ","o"},{"ờ","o"},{"ở","o"},{"ỡ","o"},{"ợ","o"},
                {"ù","u"},{"ú","u"},{"ủ","u"},{"ũ","u"},{"ụ","u"},
                {"ư","u"},{"ứ","u"},{"ừ","u"},{"ử","u"},{"ữ","u"},{"ự","u"},
                {"ỳ","y"},{"ý","y"},{"ỷ","y"},{"ỹ","y"},{"ỵ","y"},
                {"đ","d"}
            };
            foreach (var kv in map)
                slug = slug.Replace(kv.Key, kv.Value);

            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");
            slug = slug.Trim('-');
            return slug;
        }
    }
}
