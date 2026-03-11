using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POManagement.API.Models;

public class Payment
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? PaidBy { get; set; }

    [MaxLength(100)]
    public string? PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
