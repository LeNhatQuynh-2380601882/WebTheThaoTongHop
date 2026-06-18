using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.ViewModels;

namespace TamThaiTuSport.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext db, ILogger<HomeController> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Trang chủ";
            ViewData["Description"] = "Tam Thái Tử Sport - Cửa hàng thể thao chuyên nghiệp. Giày, quần áo, dụng cụ thể thao chính hãng Nike, Adidas, Yonex, Puma.";

            var vm = new HomeViewModel
            {
                Categories = await _db.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ToListAsync(),

                FeaturedProducts = await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.Stock > 0)
                    .OrderByDescending(p => p.ViewsCount)
                    .Take(8)
                    .ToListAsync(),

                NewProducts = await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.IsNew && p.Stock > 0)
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(4)
                    .ToListAsync(),

                SaleProducts = await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.DiscountPrice.HasValue && p.DiscountPrice < p.Price && p.Stock > 0)
                    .OrderByDescending(p => p.Price - p.DiscountPrice)
                    .Take(4)
                    .ToListAsync(),

                Testimonials = GetStaticTestimonials(),

                Stats = new SiteStatsViewModel
                {
                    TotalProducts = await _db.Products.CountAsync(p => p.IsActive),
                    TotalOrders = await _db.Orders.CountAsync(),
                    TotalCustomers = await _db.Users.CountAsync(),
                    YearsOfExperience = 5,
                }
            };

            return View(vm);
        }

        public async Task<IActionResult> KhuyenMai()
        {
            ViewData["Title"] = "Khuyến mãi";
            ViewData["Description"] = "Săn sale cực đỉnh với hàng ngàn sản phẩm thể thao đang giảm giá và các mã giảm giá siêu hấp dẫn tại Tam Thái Tử Sport.";

            var coupons = await _db.Coupons
                .Where(c => c.IsActive && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(c => c.DiscountValue)
                .ToListAsync();

            var saleProducts = await _db.Products
                .Include(p => p.Category)
                .Where(p => p.IsActive && p.DiscountPrice.HasValue && p.DiscountPrice < p.Price && p.Stock > 0)
                .OrderByDescending(p => p.Price - p.DiscountPrice)
                .Take(12)
                .ToListAsync();

            var vm = new PromotionsViewModel
            {
                Coupons = coupons,
                SaleProducts = saleProducts
            };

            return View(vm);
        }

        public IActionResult GioiThieu()
        {
            ViewData["Title"] = "Giới thiệu";
            ViewData["Description"] = "Tam Thái Tử Sport - Hệ thống cửa hàng đồ thể thao chính hãng, uy tín hàng đầu Việt Nam.";
            return View();
        }

        public IActionResult Wishlist()
        {
            ViewData["Title"] = "Danh sách yêu thích";
            ViewData["Description"] = "Xem danh sách sản phẩm bạn đã lưu yêu thích tại Tam Thái Tử Sport.";
            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Chính sách bảo mật";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

        private static List<TestimonialViewModel> GetStaticTestimonials() => new()
        {
            new() { Name = "Nguyễn Minh Tuấn", Location = "Hà Nội", Avatar = "NMT", Rating = 5, Comment = "Sản phẩm chất lượng tuyệt vời, giao hàng nhanh. Nike Air Zoom mình mua đã dùng 3 tháng vẫn êm như ngày đầu. Shop rất uy tín, sẽ ủng hộ dài dài!", Product = "Nike Air Zoom Pegasus 40" },
            new() { Name = "Trần Thị Mai Linh", Location = "TP. Hồ Chí Minh", Avatar = "TML", Rating = 5, Comment = "Mình rất hài lòng với vợt Yonex mua tại đây. Đóng gói cẩn thận, hàng chính hãng có tem xác nhận. Nhân viên tư vấn nhiệt tình, hiểu biết về sản phẩm.", Product = "Yonex Astrox 88D Pro" },
            new() { Name = "Lê Hoàng Phúc", Location = "Đà Nẵng", Avatar = "LHP", Rating = 5, Comment = "Đã mua đồ thể thao ở nhiều nơi nhưng Tam Thái Tử Sport là tốt nhất. Giá cạnh tranh, hàng thật 100%, và chương trình tích điểm cực kỳ có lợi cho khách hàng thân thiết.", Product = "Adidas Ultraboost 23" },
            new() { Name = "Phạm Thanh Hương", Location = "Cần Thơ", Avatar = "PTH", Rating = 4, Comment = "Mua bộ quần áo tập gym về mặc rất thoải mái, vải thoáng khí, không bí mồ hôi. Giao hàng đúng hẹn, đóng gói đẹp. Chỉ tiếc màu hơi khác ảnh một chút.", Product = "Nike Dri-FIT Training Shirt" },
        };
    }
}
