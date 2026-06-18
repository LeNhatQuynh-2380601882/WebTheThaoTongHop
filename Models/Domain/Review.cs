using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public class Review
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }

        public bool IsVerifiedPurchase { get; set; } = false;
        public int HelpfulCount { get; set; } = 0;
        public bool IsApproved { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [ForeignKey("UserId")]
        public AppUser? User { get; set; }
    }
}
