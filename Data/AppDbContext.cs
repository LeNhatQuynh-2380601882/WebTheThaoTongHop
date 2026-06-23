using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<ImportReceipt> ImportReceipts => Set<ImportReceipt>();
        public DbSet<ImportReceiptDetail> ImportReceiptDetails => Set<ImportReceiptDetail>();
        public DbSet<Coupon> Coupons => Set<Coupon>();
        public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
        public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
        public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
        public DbSet<NewsletterSubscriber> NewsletterSubscribers => Set<NewsletterSubscriber>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Rename Identity tables to Vietnamese-friendly names
            builder.Entity<AppUser>().ToTable("Users");

            // Category index
            builder.Entity<Category>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            // Product indexes
            builder.Entity<Product>()
                .HasIndex(p => p.Slug)
                .IsUnique();
            builder.Entity<Product>()
                .HasIndex(p => p.IsActive);
            builder.Entity<Product>()
                .HasIndex(p => p.CategoryId);

            // Order indexes
            builder.Entity<Order>()
                .HasIndex(o => o.UserId);
            builder.Entity<Order>()
                .HasIndex(o => o.CreatedAt);
            builder.Entity<Order>()
                .HasIndex(o => o.Status);

            // Coupon unique code
            builder.Entity<Coupon>()
                .HasIndex(c => c.Code)
                .IsUnique();

            // WishlistItem unique constraint (user + product)
            builder.Entity<WishlistItem>()
                .HasIndex(w => new { w.UserId, w.ProductId })
                .IsUnique();

            // OrderDetail computed column workaround - TotalPrice is NotMapped
            // No config needed

            // Cascade delete rules
            builder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.OrderDetails)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderDetail>()
                .HasOne(od => od.Product)
                .WithMany(p => p.OrderDetails)
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(r => r.Product)
                .WithMany(p => p.Reviews)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<WishlistItem>()
                .HasOne(w => w.User)
                .WithMany(u => u.WishlistItems)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserAddress>()
                .HasOne(a => a.User)
                .WithMany(u => u.Addresses)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<LoyaltyPoint>()
                .HasOne(lp => lp.User)
                .WithMany(u => u.LoyaltyPoints)
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ImportReceiptDetail>()
                .HasOne(d => d.ImportReceipt)
                .WithMany(r => r.Details)
                .HasForeignKey(d => d.ImportReceiptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ImportReceiptDetail>()
                .HasOne(d => d.Product)
                .WithMany(p => p.ImportReceiptDetails)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
