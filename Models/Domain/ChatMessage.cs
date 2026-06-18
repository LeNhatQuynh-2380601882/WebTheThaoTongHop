using System;
using System.ComponentModel.DataAnnotations;

namespace TamThaiTuSport.Models.Domain
{
    public class ChatMessage
    {
        public int Id { get; set; }
        
        [Required, MaxLength(100)]
        public string RoomId { get; set; } = string.Empty; // Session ID or User ID of the customer
        
        [MaxLength(450)]
        public string? SenderId { get; set; } // Null if guest customer
        
        [Required, MaxLength(100)]
        public string SenderName { get; set; } = "Khách";
        
        [Required]
        public string Message { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsAdminSender { get; set; } // True if sent by admin
    }
}
