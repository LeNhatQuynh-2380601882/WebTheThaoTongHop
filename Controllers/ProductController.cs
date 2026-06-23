using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;
using TamThaiTuSport.Models.ViewModels;

namespace TamThaiTuSport.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _db;
        private readonly UserManager<AppUser> _userManager;

        public ProductController(AppDbContext db, UserManager<AppUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /san-pham
        [Route("san-pham")]
        public async Task<IActionResult> Index(
            string? q, string? category, string? sort = "popular",
            decimal? minPrice = null, decimal? maxPrice = null, int page = 1)
        {
            const int pageSize = 16;

            var query = _db.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.Stock > 0)
                .AsQueryable();

            // Search
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    (p.Brand != null && p.Brand.ToLower().Contains(term)) ||
                    (p.Tags != null && p.Tags.ToLower().Contains(term)));
            }

            // Category filter
            Category? currentCategory = null;
            if (!string.IsNullOrWhiteSpace(category))
            {
                currentCategory = await _db.Categories.FirstOrDefaultAsync(c => c.Slug == category);
                if (currentCategory != null)
                    query = query.Where(p => p.CategoryId == currentCategory.Id);
            }

            // Price filter
            if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);

            // Sort
            query = sort switch
            {
                "price-asc" => query.OrderBy(p => p.DiscountPrice ?? p.Price),
                "price-desc" => query.OrderByDescending(p => p.DiscountPrice ?? p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                "name" => query.OrderBy(p => p.Name),
                "sale" => query.OrderByDescending(p => p.Price - (p.DiscountPrice ?? p.Price)),
                _ => query.OrderByDescending(p => p.ViewsCount) // popular
            };

            int total = await query.CountAsync();
            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new ProductListViewModel
            {
                Products = products,
                Categories = await _db.Categories.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync(),
                SearchQuery = q,
                CategorySlug = category,
                SortBy = sort,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Page = page,
                PageSize = pageSize,
                TotalItems = total,
                CurrentCategory = currentCategory
            };

            ViewData["Title"] = currentCategory != null
                ? currentCategory.Name
                : (string.IsNullOrEmpty(q) ? "Tất cả sản phẩm" : $"Kết quả: {q}");
            ViewData["SearchQuery"] = q;

            return View(vm);
        }

        // GET /san-pham/{slug}
        [Route("san-pham/{slug}")]
        public async Task<IActionResult> Detail(string slug)
        {
            var product = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Reviews).ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

            if (product == null) return NotFound();

            // Increase view count
            product.ViewsCount++;
            await _db.SaveChangesAsync();

            var relatedProducts = await _db.Products
                .Where(p => p.CategoryId == product.CategoryId && p.Id != product.Id && p.IsActive && p.Stock > 0)
                .OrderByDescending(p => p.ViewsCount)
                .Take(4)
                .ToListAsync();

            var approvedReviews = product.Reviews.Where(r => r.IsApproved).ToList();
            var vm = new ProductDetailViewModel
            {
                Product = product,
                RelatedProducts = relatedProducts,
                AverageRating = approvedReviews.Any() ? approvedReviews.Average(r => r.Rating) : 0,
                ReviewCount = approvedReviews.Count
            };

            ViewData["Title"] = product.Name;
            ViewData["Description"] = product.MetaDescription ?? product.Description?.Substring(0, Math.Min(160, product.Description?.Length ?? 0));
            return View(vm);
        }

        // GET /api/products/search?q=...&limit=5
        [HttpGet("/api/products/search")]
        public async Task<IActionResult> Search(string q, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(Array.Empty<object>());

            var term = q.Trim().ToLower();
            var products = await _db.Products
                .Where(p => p.IsActive && p.Stock > 0 &&
                    (p.Name.ToLower().Contains(term) || (p.Brand != null && p.Brand.ToLower().Contains(term))))
                .OrderByDescending(p => p.ViewsCount)
                .Take(limit)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Image,
                    p.Price,
                    FinalPrice = p.DiscountPrice.HasValue && p.DiscountPrice < p.Price ? p.DiscountPrice : p.Price,
                    p.Brand
                })
                .ToListAsync();

            return Ok(products);
        }

        // GET /api/products/get-by-ids?ids=1,2,3
        [HttpGet("/api/products/get-by-ids")]
        public async Task<IActionResult> GetByIds(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
                return Ok(Array.Empty<object>());

            var idList = ids.Split(',')
                .Select(idStr => int.TryParse(idStr, out int id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            if (!idList.Any())
                return Ok(Array.Empty<object>());

            var products = await _db.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && idList.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Image,
                    p.Price,
                    FinalPrice = p.DiscountPrice.HasValue && p.DiscountPrice < p.Price ? p.DiscountPrice.Value : p.Price,
                    DiscountPercent = p.DiscountPrice.HasValue && p.DiscountPrice < p.Price ? (int)Math.Round((p.Price - p.DiscountPrice.Value) / p.Price * 100) : 0,
                    p.Brand,
                    p.Stock,
                    CategoryIcon = p.Category != null ? p.Category.IconClass : "👟"
                })
                .ToListAsync();

            return Ok(products);
        }

        // POST /Product/AddReview
        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(int productId, int rating, string? comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var product = await _db.Products.FindAsync(productId);
            if (product == null) return NotFound();

            // Check if user already reviewed this product
            var existing = await _db.Reviews.FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == user.Id);
            if (existing != null)
            {
                TempData["Error"] = "Bạn đã đánh giá sản phẩm này rồi.";
                return Redirect($"/san-pham/{product.Slug}#reviews");
            }

            // Check if verified purchase
            var hasPurchased = await _db.Orders
                .AnyAsync(o => o.UserId == user.Id && o.Status == OrderStatus.DaGiao &&
                    o.OrderDetails.Any(d => d.ProductId == productId));

            var review = new Review
            {
                ProductId = productId,
                UserId = user.Id,
                Rating = Math.Clamp(rating, 1, 5),
                Comment = comment?.Trim(),
                IsVerifiedPurchase = hasPurchased,
                IsApproved = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reviews.Add(review);
            await _db.SaveChangesAsync();

            TempData["Success"] = "✅ Đánh giá của bạn đã được gửi. Cảm ơn!";
            return Redirect($"/san-pham/{product.Slug}#reviews");
        }

        // GET /api/products/{id}/options
        [HttpGet("/api/products/{id}/options")]
        public async Task<IActionResult> GetProductOptions(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(new { sizes = p.Sizes, colors = p.Colors });
        }
    }
}
