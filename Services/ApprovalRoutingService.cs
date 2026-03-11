using POManagement.API.Models;

namespace POManagement.API.Services;

public class ApprovalRoutingService
{
    private const decimal TwoLevelThreshold = 10000m;

    /// <summary>
    /// Determines required approval levels based on PO grand total.
    /// ≤ $10,000 → 1-level; > $10,000 → 2-level
    /// </summary>
    public int GetRequiredApprovalLevels(decimal grandTotal)
    {
        return grandTotal > TwoLevelThreshold ? 2 : 1;
    }

    /// <summary>
    /// Returns the label shown in the UI routing badge.
    /// </summary>
    public string GetApprovalRoutingLabel(decimal grandTotal)
    {
        return grandTotal > TwoLevelThreshold
            ? "2-Level Approval (Manager + Director)"
            : "1-Level Approval (Manager)";
    }

    /// <summary>
    /// Processes an approval action against a PO, advancing through levels or fully approving.
    /// Returns the new PO status after the action.
    /// </summary>
    public (POStatus newStatus, string message) ProcessApproval(PurchaseOrder po, string action, string? approverName, string? comments)
    {
        if (action.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return (POStatus.Rejected, "Purchase order has been rejected.");
        }

        if (action.Equals("Approved", StringComparison.OrdinalIgnoreCase))
        {
            int nextLevel = po.CurrentApprovalLevel + 1;

            if (nextLevel >= po.RequiredApprovalLevels)
            {
                // Fully approved — ready to be sent to vendor
                return (POStatus.Approved, "Purchase order fully approved. Ready to be sent to vendor.");
            }
            else
            {
                // Advance to next level
                return (POStatus.PendingApprovalLevel2, "Level 1 approved. Pending Level 2 approval (Director).");
            }
        }

        throw new InvalidOperationException($"Unknown approval action: {action}");
    }
}
