using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _db;

        public ReportsController(AppDbContext db)
        {
            _db = db;
        }

        // GET /admin/reports – Thống kê tổng quan
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Thống kê";

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var lastMonthStart = monthStart.AddMonths(-1);

            // Orders by status
            var statusGroups = await _db.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            // Revenue last 12 months
            var last12 = new List<object>();
            for (int i = 11; i >= 0; i--)
            {
                var m = now.AddMonths(-i);
                var mStart = new DateTime(m.Year, m.Month, 1);
                var mEnd = mStart.AddMonths(1);
                var rev = await _db.Orders
                    .Where(o => o.CreatedAt >= mStart && o.CreatedAt < mEnd && o.Status != OrderStatus.DaHuy)
                    .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0;
                var cnt = await _db.Orders.CountAsync(o => o.CreatedAt >= mStart && o.CreatedAt < mEnd);
                last12.Add(new { Label = m.ToString("MM/yyyy"), Revenue = rev, Orders = cnt });
            }

            // Top selling products (by order detail quantity)
            var topSelling = await _db.OrderDetails
                .Include(d => d.Product)
                .GroupBy(d => d.ProductId)
                .Select(g => new {
                    ProductId = g.Key,
                    ProductName = g.First().Product!.Name,
                    Image = g.First().Product!.Image,
                    TotalQty = g.Sum(d => d.Quantity),
                    TotalRevenue = g.Sum(d => (decimal)d.Quantity * d.UnitPrice)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(10)
                .ToListAsync();

            // New customers per month (last 6)
            var newCustomers = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var m = now.AddMonths(-i);
                var mStart = new DateTime(m.Year, m.Month, 1);
                var mEnd = mStart.AddMonths(1);
                var cnt = await _db.Users.CountAsync(u => u.CreatedAt >= mStart && u.CreatedAt < mEnd);
                newCustomers.Add(new { Label = m.ToString("MM/yyyy"), Count = cnt });
            }

            // Category revenue
            var categoryRevenue = await _db.OrderDetails
                .Include(d => d.Product).ThenInclude(p => p!.Category)
                .GroupBy(d => d.Product!.Category!.Name)
                .Select(g => new { Category = g.Key, Revenue = g.Sum(d => (decimal)d.Quantity * d.UnitPrice) })
                .OrderByDescending(x => x.Revenue)
                .Take(6)
                .ToListAsync();

            ViewBag.StatusGroups = statusGroups;
            ViewBag.Last12Months = last12;
            ViewBag.TopSelling = topSelling;
            ViewBag.NewCustomers = newCustomers;
            ViewBag.CategoryRevenue = categoryRevenue;
            ViewBag.TotalRevenue = await _db.Orders
                .Where(o => o.Status != OrderStatus.DaHuy)
                .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0;
            ViewBag.TotalOrders = await _db.Orders.CountAsync();
            ViewBag.TotalCustomers = await _db.Users.CountAsync();

            return View();
        }

        // GET /admin/reports/revenue – Doanh thu chi tiết
        public async Task<IActionResult> Revenue(int year = 0, int month = 0)
        {
            ViewData["Title"] = "Báo cáo doanh thu";

            if (year == 0) year = DateTime.UtcNow.Year;
            if (month == 0) month = DateTime.UtcNow.Month;

            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            // Daily revenue for selected month
            var dailyRevenue = new List<object>();
            for (int d = 1; d <= DateTime.DaysInMonth(year, month); d++)
            {
                var dayStart = new DateTime(year, month, d);
                var dayEnd = dayStart.AddDays(1);
                var rev = await _db.Orders
                    .Where(o => o.CreatedAt >= dayStart && o.CreatedAt < dayEnd && o.Status != OrderStatus.DaHuy)
                    .SumAsync(o => (decimal?)(o.TotalAmount + o.ShippingFee - o.DiscountAmount)) ?? 0;
                var cnt = await _db.Orders.CountAsync(o => o.CreatedAt >= dayStart && o.CreatedAt < dayEnd);
                dailyRevenue.Add(new { Day = d, Revenue = rev, Orders = cnt });
            }

            // Month orders
            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.CreatedAt >= start && o.CreatedAt < end)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // Summary
            var totalRevenue = orders.Where(o => o.Status != OrderStatus.DaHuy)
                .Sum(o => o.TotalAmount + o.ShippingFee - o.DiscountAmount);
            var totalOrders = orders.Count;
            var completedOrders = orders.Count(o => o.Status == OrderStatus.DaGiao);
            var cancelledOrders = orders.Count(o => o.Status == OrderStatus.DaHuy);

            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.DailyRevenue = dailyRevenue;
            ViewBag.Orders = orders;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.CompletedOrders = completedOrders;
            ViewBag.CancelledOrders = cancelledOrders;
            ViewBag.AvgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

            // Available years
            var minYear = await _db.Orders.MinAsync(o => (DateTime?)o.CreatedAt);
            ViewBag.Years = Enumerable.Range(minYear?.Year ?? DateTime.UtcNow.Year, DateTime.UtcNow.Year - (minYear?.Year ?? DateTime.UtcNow.Year) + 1).ToList();

            return View();
        }
    }
}
