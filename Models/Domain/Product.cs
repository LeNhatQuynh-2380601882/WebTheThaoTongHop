using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public class Product
    {
        public int Id { get; set; }

        public int? CategoryId { get; set; }

        [Required, MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Slug { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiscountPrice { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? Image { get; set; }

        public int Stock { get; set; } = 0;

        [MaxLength(255)]
        public string? Sizes { get; set; } = "S,M,L,XL";

        [MaxLength(255)]
        public string? Colors { get; set; } = "Đen,Trắng,Xanh,Đỏ";

        public bool IsNew { get; set; } = false;

        [MaxLength(255)]
        public string? Supplier { get; set; }

        [MaxLength(100)]
        public string? Brand { get; set; }

        [MaxLength(255)]
        public string? Tags { get; set; }

        [MaxLength(150)]
        public string? MetaTitle { get; set; }

        [MaxLength(300)]
        public string? MetaDescription { get; set; }

        public int ViewsCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<ImportReceiptDetail> ImportReceiptDetails { get; set; } = new List<ImportReceiptDetail>();
        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        // Computed
        [NotMapped]
        public decimal FinalPrice => DiscountPrice.HasValue && DiscountPrice < Price ? DiscountPrice.Value : Price;

        [NotMapped]
        public int DiscountPercent => DiscountPrice.HasValue && DiscountPrice < Price
            ? (int)Math.Round((Price - DiscountPrice.Value) / Price * 100) : 0;

        [NotMapped]
        public List<string> SizeList => Sizes?.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList() ?? new();

        [NotMapped]
        public List<string> ColorList => Colors?.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList() ?? new();
    }
}
