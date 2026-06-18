using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TamThaiTuSport.Models.Domain
{
    public class ImportReceipt
    {
        public int Id { get; set; }

        [MaxLength(450)]
        public string? CreatedByUserId { get; set; }

        [MaxLength(200)]
        public string? SupplierName { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalCost { get; set; }

        public DateTime ImportDate { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("CreatedByUserId")]
        public AppUser? CreatedBy { get; set; }

        public ICollection<ImportReceiptDetail> Details { get; set; } = new List<ImportReceiptDetail>();
    }

    public class ImportReceiptDetail
    {
        public int Id { get; set; }

        public int ImportReceiptId { get; set; }
        public int ProductId { get; set; }

        public int Quantity { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal CostPrice { get; set; }

        // Navigation
        [ForeignKey("ImportReceiptId")]
        public ImportReceipt ImportReceipt { get; set; } = null!;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }
}
