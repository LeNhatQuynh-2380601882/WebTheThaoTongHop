using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Product> FeaturedProducts { get; set; } = new();
        public List<Product> NewProducts { get; set; } = new();
        public List<Product> SaleProducts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public List<TestimonialViewModel> Testimonials { get; set; } = new();
        public SiteStatsViewModel Stats { get; set; } = new();
    }

    public class TestimonialViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Avatar { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
    }

    public class SiteStatsViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalCustomers { get; set; }
        public int YearsOfExperience { get; set; } = 5;
    }

    public class ProductListViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
        public string? SearchQuery { get; set; }
        public string? CategorySlug { get; set; }
        public string? SortBy { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 16;
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPrevPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
        public Category? CurrentCategory { get; set; }
    }

    public class ProductDetailViewModel
    {
        public Product Product { get; set; } = null!;
        public List<Product> RelatedProducts { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
    }

    public class CheckoutViewModel
    {
        public List<CartItemViewModel> CartItems { get; set; } = new();
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerEmail { get; set; }
        public string? ShippingAddress { get; set; }
        public string? Province { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string PaymentMethod { get; set; } = "COD";
        public string? CouponCode { get; set; }
        public string? Note { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal Total => Subtotal + ShippingFee - Discount;
    }

    public class DashboardViewModel
    {
        public decimal TodayRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public int TodayOrders { get; set; }
        public int PendingOrders { get; set; }
        public int TotalProducts { get; set; }
        public int LowStockProducts { get; set; }
        public int TotalCustomers { get; set; }
        public List<RevenueChartPoint> RevenueChart { get; set; } = new();
        public List<Order> RecentOrders { get; set; } = new();
        public List<Product> TopProducts { get; set; } = new();
    }

    public class RevenueChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class PromotionsViewModel
    {
        public List<Coupon> Coupons { get; set; } = new();
        public List<Product> SaleProducts { get; set; } = new();
    }
}
