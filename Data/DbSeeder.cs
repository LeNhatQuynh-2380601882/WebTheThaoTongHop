using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Apply pending migrations
            await context.Database.MigrateAsync();

            // Seed Roles
            var roles = new[] { "Admin", "NhanVien", "Customer" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed Admin user
            if (await userManager.FindByEmailAsync("admin@tamthaitu.vn") == null)
            {
                var admin = new AppUser
                {
                    UserName = "admin@tamthaitu.vn",
                    Email = "admin@tamthaitu.vn",
                    FullName = "Admin Tam Thái Tử",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(admin, "Admin@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Seed Customer user
            if (await userManager.FindByEmailAsync("user@tamthaitu.vn") == null)
            {
                var customer = new AppUser
                {
                    UserName = "user@tamthaitu.vn",
                    Email = "user@tamthaitu.vn",
                    FullName = "Khách Hàng Tam Thái Tử",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(customer, "User@123456");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(customer, "Customer");
            }

            // Seed Categories
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new() { Name = "Giày Thể Thao", Slug = "giay-the-thao", IconClass = "🏃", Color = "#f97316", SortOrder = 1 },
                    new() { Name = "Quần Áo Thể Thao", Slug = "quan-ao-the-thao", IconClass = "👕", Color = "#3b82f6", SortOrder = 2 },
                    new() { Name = "Bóng Đá", Slug = "bong-da", IconClass = "⚽", Color = "#22c55e", SortOrder = 3 },
                    new() { Name = "Bóng Rổ", Slug = "bong-ro", IconClass = "🏀", Color = "#ef4444", SortOrder = 4 },
                    new() { Name = "Cầu Lông", Slug = "cau-long", IconClass = "🏸", Color = "#a855f7", SortOrder = 5 },
                    new() { Name = "Bơi Lội", Slug = "boi-loi", IconClass = "🏊", Color = "#06b6d4", SortOrder = 6 },
                    new() { Name = "Tennis", Slug = "tennis", IconClass = "🎾", Color = "#eab308", SortOrder = 7 },
                    new() { Name = "Gym & Fitness", Slug = "gym-fitness", IconClass = "🏋️", Color = "#64748b", SortOrder = 8 },
                };
                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }

            // Seed Products
            if (!context.Products.Any())
            {
                var categories = await context.Categories.ToListAsync();
                var catMap = categories.ToDictionary(c => c.Slug!, c => c.Id);

                var products = new List<Product>
                {
                    // Giày
                    new() { CategoryId = catMap["giay-the-thao"], Name = "Nike Air Zoom Pegasus 40", Slug = "nike-air-zoom-pegasus-40", Price = 2890000, DiscountPrice = 2490000, Brand = "Nike", Stock = 50, IsNew = true, Sizes = "38,39,40,41,42,43,44", Colors = "Đen,Trắng,Xanh Navy", Tags = "chạy bộ,nike,thoáng khí", Description = "Giày chạy bộ cao cấp với đệm Air Zoom siêu êm, thiết kế khí động học tối ưu hiệu suất. Phù hợp cho chạy đường dài và tập gym hàng ngày.", MetaTitle = "Nike Air Zoom Pegasus 40 - Giày Chạy Bộ Cao Cấp", IsActive = true },
                    new() { CategoryId = catMap["giay-the-thao"], Name = "Adidas Ultraboost 23", Slug = "adidas-ultraboost-23", Price = 3290000, DiscountPrice = 2790000, Brand = "Adidas", Stock = 35, IsNew = true, Sizes = "39,40,41,42,43,44", Colors = "Trắng,Đen,Xám", Tags = "chạy bộ,adidas,boost", Description = "Công nghệ Boost mang lại năng lượng hoàn hảo cho mỗi bước chân. Thiết kế Primeknit ôm sát bàn chân.", IsActive = true },
                    new() { CategoryId = catMap["giay-the-thao"], Name = "Asics Gel-Kayano 30", Slug = "asics-gel-kayano-30", Price = 3490000, Brand = "Asics", Stock = 20, Sizes = "38,39,40,41,42,43", Colors = "Đỏ,Đen,Xanh", Tags = "chạy bộ,asics,gel", Description = "Công nghệ GEL hấp thụ xung động vượt trội, hỗ trợ vòm bàn chân tối ưu cho người chạy dài hàng ngày.", IsActive = true },
                    new() { CategoryId = catMap["giay-the-thao"], Name = "Puma RS-X Efekt", Slug = "puma-rs-x-efekt", Price = 1890000, DiscountPrice = 1590000, Brand = "Puma", Stock = 40, Sizes = "38,39,40,41,42,43,44", Colors = "Trắng,Đen,Cam", Tags = "lifestyle,puma,sneaker", Description = "Phong cách retro mạnh mẽ kết hợp công nghệ RS hiện đại. Đế RS foam siêu nhẹ và êm ái.", IsActive = true },

                    // Quần áo
                    new() { CategoryId = catMap["quan-ao-the-thao"], Name = "Nike Dri-FIT Training Shirt", Slug = "nike-dri-fit-training-shirt", Price = 590000, DiscountPrice = 490000, Brand = "Nike", Stock = 100, IsNew = true, Sizes = "S,M,L,XL,XXL", Colors = "Đen,Trắng,Xanh,Đỏ,Xám", Tags = "áo thun,nike,dri-fit,tập gym", Description = "Công nghệ Dri-FIT thấm hút mồ hôi cực kỳ hiệu quả. Vải thoáng khí, co giãn 4 chiều.", IsActive = true },
                    new() { CategoryId = catMap["quan-ao-the-thao"], Name = "Adidas Tiro 23 Training Pants", Slug = "adidas-tiro-23-training-pants", Price = 790000, Brand = "Adidas", Stock = 75, Sizes = "S,M,L,XL", Colors = "Đen,Navy,Xanh lá", Tags = "quần dài,adidas,training", Description = "Quần training chuyên nghiệp với thiết kế tapered hiện đại. Túi có khóa zip tiện dụng.", IsActive = true },
                    new() { CategoryId = catMap["quan-ao-the-thao"], Name = "Under Armour HeatGear Compression", Slug = "under-armour-heatgear-compression", Price = 890000, DiscountPrice = 750000, Brand = "Under Armour", Stock = 60, Sizes = "S,M,L,XL", Colors = "Đen,Xanh Navy,Đỏ", Tags = "compression,under armour,heatgear", Description = "Áo compression HeatGear giúp duy trì nhiệt độ cơ thể tối ưu. Hỗ trợ cơ bắp và giảm mệt mỏi.", IsActive = true },

                    // Bóng đá
                    new() { CategoryId = catMap["bong-da"], Name = "Adidas World Cup 2022 Football", Slug = "adidas-world-cup-2022-football", Price = 1290000, DiscountPrice = 990000, Brand = "Adidas", Stock = 30, Tags = "bóng đá,adidas,world cup", Description = "Bóng thi đấu chính thức World Cup 2022 với công nghệ Connected Ball Technology. Đường may nhiệt không thấm nước.", IsActive = true },
                    new() { CategoryId = catMap["bong-da"], Name = "Nike Mercurial Vapor 15 Elite", Slug = "nike-mercurial-vapor-15-elite", Price = 5990000, DiscountPrice = 4990000, Brand = "Nike", Stock = 15, IsNew = true, Sizes = "38,39,40,41,42,43,44,45", Colors = "Vàng,Đen,Đỏ", Tags = "giày đá banh,nike,mercurial", Description = "Giày đá banh cao cấp nhất của Nike với đế ACC và mũi giày tinh chỉnh. Dành cho cầu thủ chuyên nghiệp.", IsActive = true },
                    new() { CategoryId = catMap["bong-da"], Name = "Găng Tay Thủ Môn Adidas Predator", Slug = "gang-tay-thu-mon-adidas-predator", Price = 890000, Brand = "Adidas", Stock = 25, Tags = "thủ môn,adidas,predator", Description = "Găng tay thủ môn chuyên nghiệp với foam CONTACT PRO siêu bám. Hệ thống strap điều chỉnh dễ dàng.", IsActive = true },

                    // Bóng rổ
                    new() { CategoryId = catMap["bong-ro"], Name = "Nike Air Jordan 1 Mid", Slug = "nike-air-jordan-1-mid", Price = 3490000, Brand = "Nike", Stock = 20, IsNew = true, Sizes = "38,39,40,41,42,43,44,45", Colors = "Đen/Trắng,Đỏ/Đen,Chicago", Tags = "bóng rổ,jordan,nike", Description = "Huyền thoại Air Jordan 1 Mid với thiết kế cổ mid iconic. Đế Air đệm hoàn hảo cho sân bóng rổ.", IsActive = true },
                    new() { CategoryId = catMap["bong-ro"], Name = "Spalding NBA Official Game Ball", Slug = "spalding-nba-official-game-ball", Price = 1590000, DiscountPrice = 1290000, Brand = "Spalding", Stock = 30, Tags = "bóng rổ,spalding,nba", Description = "Bóng rổ chính thức NBA với da composite cao cấp. Đường khâu nổi bật giúp kiểm soát bóng tốt hơn.", IsActive = true },

                    // Cầu lông
                    new() { CategoryId = catMap["cau-long"], Name = "Yonex Astrox 88D Pro", Slug = "yonex-astrox-88d-pro", Price = 4290000, Brand = "Yonex", Stock = 12, IsNew = true, Tags = "cầu lông,yonex,vợt", Description = "Vợt cầu lông chuyên tấn công với Head-heavy balance. Khung Carbon Nanotube siêu cứng và nhẹ.", IsActive = true },
                    new() { CategoryId = catMap["cau-long"], Name = "Victor Thruster K 90S", Slug = "victor-thruster-k-90s", Price = 2890000, DiscountPrice = 2490000, Brand = "Victor", Stock = 18, Tags = "cầu lông,victor,vợt", Description = "Vợt cầu lông Victor cao cấp cho người chơi chuyên nghiệp. Điểm ngọt rộng, phù hợp đánh toàn sân.", IsActive = true },

                    // Gym
                    new() { CategoryId = catMap["gym-fitness"], Name = "Bộ Tạ Tay Cao Su 20kg", Slug = "bo-ta-tay-cao-su-20kg", Price = 1890000, DiscountPrice = 1590000, Stock = 20, Tags = "tạ tay,gym,fitness", Description = "Bộ tạ tay bọc cao su chống trượt, không gây tiếng ồn. Phù hợp tập tại nhà và phòng gym.", IsActive = true },
                    new() { CategoryId = catMap["gym-fitness"], Name = "Dây Kháng Lực Resistance Band Set", Slug = "day-khang-luc-resistance-band-set", Price = 390000, DiscountPrice = 290000, Stock = 80, IsNew = true, Tags = "resistance band,gym,tập nhà", Description = "Bộ 5 dây kháng lực đa màu với 5 mức độ kháng khác nhau. Chất liệu latex tự nhiên bền bỉ.", IsActive = true },
                };

                context.Products.AddRange(products);
                await context.SaveChangesAsync();
            }

            // Seed Coupons
            if (!context.Coupons.Any())
            {
                var coupons = new List<Coupon>
                {
                    new() { Code = "WELCOME10", Description = "Giảm 10% cho đơn hàng đầu tiên", IsPercent = true, DiscountValue = 10, MaxDiscount = 200000, MaxUsage = 1000, ExpiresAt = DateTime.UtcNow.AddMonths(6), IsActive = true },
                    new() { Code = "SUMMER50K", Description = "Giảm 50.000đ cho đơn từ 500.000đ", IsPercent = false, DiscountValue = 50000, MinOrderAmount = 500000, MaxUsage = 500, ExpiresAt = DateTime.UtcNow.AddMonths(3), IsActive = true },
                    new() { Code = "VIP20", Description = "Giảm 20% dành riêng cho VIP", IsPercent = true, DiscountValue = 20, MaxDiscount = 500000, MaxUsage = 100, ExpiresAt = DateTime.UtcNow.AddMonths(12), IsActive = true },
                };
                context.Coupons.AddRange(coupons);
                await context.SaveChangesAsync();
            }
        }
    }
}
