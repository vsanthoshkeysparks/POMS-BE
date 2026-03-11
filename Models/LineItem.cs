using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POManagement.API.Models;

public class LineItem
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
