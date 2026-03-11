using POManagement.API.Models;

namespace POManagement.API.Services;

/// <summary>
/// Centralized workflow transition management for the PO lifecycle.
/// Prevents workflow steps from being skipped and enforces the correct sequence.
/// 
/// Full workflow sequence:
///   Draft → PendingApprovalLevel1 → (PendingApprovalLevel2 if > $10k) →
///   Approved → SentToVendor → MaterialDispatched →
///   VerificationPending → FinalApproved → Completed
/// </summary>
public class WorkflowService
{
    // Allowed transitions map: from → set of allowed destinations
    private static readonly Dictionary<POStatus, HashSet<POStatus>> AllowedTransitions = new()
    {
        [POStatus.Draft]                 = new() { POStatus.PendingApprovalLevel1 },
        [POStatus.PendingApprovalLevel1] = new() { POStatus.PendingApprovalLevel2, POStatus.Approved, POStatus.Rejected },
        [POStatus.PendingApprovalLevel2] = new() { POStatus.Approved, POStatus.Rejected },
        [POStatus.Approved]              = new() { POStatus.SentToVendor },
        [POStatus.SentToVendor]          = new() { POStatus.MaterialDispatched },
        [POStatus.MaterialDispatched]    = new() { POStatus.VerificationPending },
        [POStatus.VerificationPending]   = new() { POStatus.FinalApproved },
        [POStatus.FinalApproved]         = new() { POStatus.PaymentProcessing },
        [POStatus.PaymentProcessing]     = new() { POStatus.Completed },
        [POStatus.Completed]             = new(),   // terminal
        [POStatus.Rejected]              = new(),   // terminal
    };

    /// <summary>
    /// Returns true if the transition from → to is allowed in the workflow.
    /// </summary>
    public bool CanTransition(POStatus from, POStatus to)
        => AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>
    /// Returns the human-readable label for the current workflow stage.
    /// </summary>
    public string GetStageLabel(POStatus status) => status switch
    {
        POStatus.Draft                 => "Draft",
        POStatus.PendingApprovalLevel1 => "Pending Level 1 Approval",
        POStatus.PendingApprovalLevel2 => "Pending Level 2 Approval (Director)",
        POStatus.Approved              => "Approved — Ready for Vendor",
        POStatus.SentToVendor          => "Sent to Vendor",
        POStatus.MaterialDispatched    => "Material Dispatched (In Transit)",
        POStatus.VerificationPending   => "Goods Received — Pending Final Approval",
        POStatus.FinalApproved         => "Final Approved — Awaiting Payment",
        POStatus.PaymentProcessing     => "Payment Processing",
        POStatus.Completed             => "Completed",
        POStatus.Rejected              => "Rejected",
        _ => status.ToString()
    };

    /// <summary>
    /// Returns the next expected action label for the current stage.
    /// </summary>
    public string GetNextAction(POStatus status) => status switch
    {
        POStatus.Draft                 => "Submit for Approval",
        POStatus.PendingApprovalLevel1 => "Manager must Approve or Reject",
        POStatus.PendingApprovalLevel2 => "Director must Approve or Reject",
        POStatus.Approved              => "Send PO to Vendor",
        POStatus.SentToVendor          => "Vendor must Dispatch Material",
        POStatus.MaterialDispatched    => "Acknowledge Goods Receipt",
        POStatus.VerificationPending   => "Final Approval required before Payment",
        POStatus.FinalApproved         => "Process Payment",
        POStatus.PaymentProcessing     => "Awaiting payment confirmation",
        POStatus.Completed             => "No further action — PO is Completed",
        POStatus.Rejected              => "No further action — PO has been Rejected",
        _ => "Unknown"
    };

    /// <summary>
    /// Returns the workflow stage number (1–8) for the current status.
    /// </summary>
    public int GetStageNumber(POStatus status) => status switch
    {
        POStatus.Draft                 => 1,
        POStatus.PendingApprovalLevel1 => 2,
        POStatus.PendingApprovalLevel2 => 3,
        POStatus.Approved              => 4,
        POStatus.SentToVendor          => 5,
        POStatus.MaterialDispatched    => 6,
        POStatus.VerificationPending   => 7,
        POStatus.FinalApproved         => 8,
        POStatus.PaymentProcessing     => 8,
        POStatus.Completed             => 8,
        POStatus.Rejected              => 0,
        _ => 0
    };

    /// <summary>
    /// Total workflow stages (depends on approval levels).
    /// </summary>
    public int GetTotalStages(int requiredApprovalLevels)
        => requiredApprovalLevels == 2 ? 9 : 8; // extra stage for Level 2 approval

    /// <summary>
    /// Builds a complete workflow summary DTO for a PO.
    /// </summary>
    public WorkflowSummaryDto BuildSummary(PurchaseOrder po)
    {
        return new WorkflowSummaryDto
        {
            PoId = po.Id,
            PoNumber = po.PONumber,
            VendorName = po.VendorName,
            TotalAmount = po.TotalAmount,
            CurrentStatus = po.Status.ToString(),
            StageLabel = GetStageLabel(po.Status),
            NextAction = GetNextAction(po.Status),
            CurrentStage = GetStageNumber(po.Status),
            TotalStages = GetTotalStages(po.RequiredApprovalLevels),
            RequiredApprovalLevels = po.RequiredApprovalLevels,
            CurrentApprovalLevel = po.CurrentApprovalLevel,
            IsTerminal = po.Status is POStatus.Completed or POStatus.Rejected,
            IsApprovalPending = po.Status is POStatus.PendingApprovalLevel1 or POStatus.PendingApprovalLevel2,
            CreatedAt = po.CreatedAt,
            UpdatedAt = po.UpdatedAt,
            ApprovalHistory = po.ApprovalLogs
                .OrderBy(l => l.ActionDate)
                .Select(l => new ApprovalHistoryEntry
                {
                    Level = l.ApprovalLevel,
                    Action = l.Action,
                    ApproverName = l.ApproverName,
                    Comments = l.Comments,
                    ActionDate = l.ActionDate
                }).ToList()
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class WorkflowSummaryDto
{
    public int PoId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    public string StageLabel { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public int TotalStages { get; set; }
    public int RequiredApprovalLevels { get; set; }
    public int CurrentApprovalLevel { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsApprovalPending { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ApprovalHistoryEntry> ApprovalHistory { get; set; } = new();
}

public class ApprovalHistoryEntry
{
    public int Level { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
    public DateTime ActionDate { get; set; }
}
