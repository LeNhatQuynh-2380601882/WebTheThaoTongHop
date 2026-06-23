using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public enum OrderStatus
    {
        ChoPhanHoi = 0,    // Chờ phản hồi
        DaXacNhan = 1,     // Đã xác nhận
        DangGiao = 2,      // Đang giao
        DaGiao = 3,        // Đã giao
        DaHuy = 4          // Đã hủy
    }

    public enum PaymentMethod
    {
        COD = 0,
        ChuyenKhoan = 1,
        MoMo = 2,
        ZaloPay = 3,
        VNPay = 4
    }

    public enum PaymentStatus
    {
        ChuaThanhToan = 0,
        DaThanhToan = 1,
        HoanTien = 2
    }

    public class Order
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        // Thông tin khách hàng (lưu tại thời điểm đặt)
        [Required, MaxLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? CustomerPhone { get; set; }

        [MaxLength(200)]
        public string? CustomerEmail { get; set; }

        [MaxLength(500)]
        public string? ShippingAddress { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal ShippingFee { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        public int? CouponId { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.ChoPhanHoi;
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.ChuaThanhToan;

        [MaxLength(100)]
        public string? TrackingCode { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("UserId")]
        public AppUser? User { get; set; }

        [ForeignKey("CouponId")]
        public Coupon? Coupon { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

        // Computed
        [NotMapped]
        public decimal FinalTotal => TotalAmount + ShippingFee - DiscountAmount;
    }
}
