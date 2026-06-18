using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProductsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // GET /admin/products
        public async Task<IActionResult> Index(string? q, int? categoryId, int page = 1)
        {
            ViewData["Title"] = "Quản lý sản phẩm";
            const int pageSize = 20;

            var query = _db.Products.Include(p => p.Category).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(p => p.Name.Contains(q) || (p.Brand != null && p.Brand.Contains(q)));
            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId);

            int total = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.SearchQuery = q;
            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
            ViewBag.CategoryId = categoryId;

            return View(products);
        }

        // GET /admin/products/create
        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "Thêm sản phẩm";
            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
            return View(new Product());
        }

        // POST /admin/products/create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsDir);
                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);
                product.Image = fileName;
            }

            // Auto generate slug
            if (string.IsNullOrEmpty(product.Slug))
                product.Slug = GenerateSlug(product.Name);

            product.CreatedAt = DateTime.UtcNow;
            product.UpdatedAt = DateTime.UtcNow;
            product.IsActive = true;

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Thêm sản phẩm thành công.";
            return RedirectToAction("Index");
        }

        // GET /admin/products/edit/{id}
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();
            ViewData["Title"] = "Chỉnh sửa sản phẩm";
            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
            return View(product);
        }

        // POST /admin/products/edit/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? imageFile)
        {
            ViewBag.Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();

            var existing = await _db.Products.FindAsync(id);
            if (existing == null) return NotFound();

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsDir);
                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"{Guid.NewGuid()}{ext}";
                using var stream = new FileStream(Path.Combine(uploadsDir, fileName), FileMode.Create);
                await imageFile.CopyToAsync(stream);
                existing.Image = fileName;
            }

            existing.Name = product.Name;
            existing.Slug = string.IsNullOrEmpty(product.Slug) ? GenerateSlug(product.Name) : product.Slug;
            existing.CategoryId = product.CategoryId;
            existing.Brand = product.Brand;
            existing.Price = product.Price;
            existing.DiscountPrice = product.DiscountPrice;
            existing.Description = product.Description;
            existing.Stock = product.Stock;
            existing.Sizes = product.Sizes;
            existing.Colors = product.Colors;
            existing.Tags = product.Tags;
            existing.IsNew = product.IsNew;
            existing.IsActive = product.IsActive;
            existing.MetaTitle = product.MetaTitle;
            existing.MetaDescription = product.MetaDescription;
            existing.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["Success"] = "✅ Cập nhật sản phẩm thành công.";
            return RedirectToAction("Index");
        }

        // POST /admin/products/delete/{id}
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _db.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsActive = false; // Soft delete
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Đã ẩn sản phẩm.";
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
