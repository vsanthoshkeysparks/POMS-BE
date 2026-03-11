using System.ComponentModel.DataAnnotations;

namespace POManagement.API.Models;

public class MaterialReceipt
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? ReceivedBy { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Final verification fields
    public DateTime? VerifiedDate { get; set; }

    [MaxLength(200)]
    public string? VerifiedBy { get; set; }

    [MaxLength(500)]
    public string? VerificationNotes { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
