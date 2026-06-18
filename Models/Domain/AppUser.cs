using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TamThaiTuSport.Models.Domain
{
    public class AppUser : IdentityUser
    {
        [MaxLength(100)]
        public string? FullName { get; set; }

        [MaxLength(500)]
        public string? Avatar { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public int LoyaltyPointsTotal { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime? LastLogin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
        public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
    }
}
