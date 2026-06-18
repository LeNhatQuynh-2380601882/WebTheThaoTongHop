using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public class Coupon
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsPercent { get; set; } = true; // true = %, false = fixed amount

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? MinOrderAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxDiscount { get; set; }

        public int? MaxUsage { get; set; }
        public int UsedCount { get; set; } = 0;

        public DateTime? ExpiresAt { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        // Computed
        [NotMapped]
        public bool IsValid => IsActive
            && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow)
            && (MaxUsage == null || UsedCount < MaxUsage);
    }
}
