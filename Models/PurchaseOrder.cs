using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POManagement.API.Models;

public enum POStatus
{
    Draft = 0,
    PendingApprovalLevel1 = 1,
    PendingApprovalLevel2 = 2,
    Approved = 3,
    SentToVendor = 4,
    MaterialDispatched = 5,
    MaterialReceived = 6,
    VerificationPending = 7,
    FinalApproved = 8,
    PaymentProcessing = 9,
    Completed = 10,
    Rejected = 11
}

public class PurchaseOrder
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string PONumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string VendorName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string PurchaseType { get; set; } = string.Empty;

    public DateTime PODate { get; set; }

    public POStatus Status { get; set; } = POStatus.Draft;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    
    public int RequiredApprovalLevels { get; set; } = 1;
    public int CurrentApprovalLevel { get; set; } = 0;

    [MaxLength(200)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    public ICollection<LineItem> LineItems { get; set; } = new List<LineItem>();
    public ICollection<ApprovalLog> ApprovalLogs { get; set; } = new List<ApprovalLog>();
    public MaterialReceipt? MaterialReceipt { get; set; }
    public Payment? Payment { get; set; }
}
