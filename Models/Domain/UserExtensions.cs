using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public class WishlistItem
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public int ProductId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }

    public class UserAddress
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string ReceiverName { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Province { get; set; }

        [MaxLength(100)]
        public string? District { get; set; }

        [MaxLength(100)]
        public string? Ward { get; set; }

        public bool IsDefault { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;
    }

    public class LoyaltyPoint
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public int Points { get; set; } // positive = earn, negative = redeem

        [MaxLength(200)]
        public string? Description { get; set; }

        public int? OrderId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public AppUser User { get; set; } = null!;
    }

    public class NewsletterSubscriber
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
