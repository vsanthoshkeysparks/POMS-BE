using System.ComponentModel.DataAnnotations;

namespace POManagement.API.Models;

public class ApprovalLog
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }

    public int ApprovalLevel { get; set; }  // 1 or 2

    [MaxLength(200)]
    public string? ApproverName { get; set; }

    [Required]
    [MaxLength(20)]
    public string Action { get; set; } = string.Empty;  // "Approved" or "Rejected"

    [MaxLength(500)]
    public string? Comments { get; set; }

    public DateTime ActionDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
