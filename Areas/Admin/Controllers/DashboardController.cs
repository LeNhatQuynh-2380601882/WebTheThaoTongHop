using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;
using TamThaiTuSport.Models.ViewModels;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DashboardController : Controller
    {
        private readonly AppDbContext _db;

        public DashboardController(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Dashboard Admin";

            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var vm = new DashboardViewModel
            {
                TodayRevenue = await _db.Orders
                    .Where(o => o.CreatedAt >= todayStart && o.Status != OrderStatus.DaHuy)
                    .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0,

                MonthRevenue = await _db.Orders
                    .Where(o => o.CreatedAt >= monthStart && o.Status != OrderStatus.DaHuy)
                    .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0,

                TodayOrders = await _db.Orders.CountAsync(o => o.CreatedAt >= todayStart),
                PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.ChoPhanHoi),
                TotalProducts = await _db.Products.CountAsync(p => p.IsActive),
                LowStockProducts = await _db.Products.CountAsync(p => p.IsActive && p.Stock < 10 && p.Stock > 0),
                TotalCustomers = await _db.Users.CountAsync(),

                RevenueChart = await GetRevenueChart(),

                RecentOrders = await _db.Orders
                    .Include(o => o.OrderDetails)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .ToListAsync(),

                TopProducts = await _db.Products
                    .Where(p => p.IsActive)
                    .OrderByDescending(p => p.ViewsCount)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }

        private async Task<List<RevenueChartPoint>> GetRevenueChart()
        {
            var points = new List<RevenueChartPoint>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var revenue = await _db.Orders
                    .Where(o => o.CreatedAt.Date == date && o.Status != OrderStatus.DaHuy)
                    .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0;

                points.Add(new RevenueChartPoint
                {
                    Label = date.ToString("dd/MM"),
                    Value = revenue
                });
            }
            return points;
        }
    }
}
