using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Models;
using POManagement.API.Services;

namespace POManagement.API.Controllers;

/// <summary>
/// Workflow controller — read-only visibility into PO lifecycle stages.
/// Mutations happen through the dedicated controllers (Approvals, MaterialReceipt, Payment, etc.)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkflowService _workflow;

    public WorkflowController(AppDbContext db, WorkflowService workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    /// <summary>
    /// GET /api/workflow/{id}/status
    /// Returns stage label, next action, current stage, total stages, and approval history.
    /// </summary>
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.ApprovalLogs)
            .Include(p => p.MaterialReceipt)
            .Include(p => p.Payment)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();

        return Ok(_workflow.BuildSummary(po));
    }

    /// <summary>
    /// GET /api/workflow/{id}/history
    /// Returns a chronological audit trail of all status events for a PO.
    /// </summary>
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.ApprovalLogs)
            .Include(p => p.MaterialReceipt)
            .Include(p => p.Payment)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();

        var timeline = new List<object>();

        // Creation event
        timeline.Add(new
        {
            step = 1,
            stage = "PO Created",
            status = "Draft",
            actor = po.CreatedBy ?? "System",
            timestamp = po.CreatedAt,
            notes = po.Notes
        });

        // Approval events
        foreach (var log in po.ApprovalLogs.OrderBy(l => l.ActionDate))
        {
            timeline.Add(new
            {
                step = 1 + log.ApprovalLevel,
                stage = $"Approval Level {log.ApprovalLevel}",
                status = log.Action,
                actor = log.ApproverName ?? "Unknown",
                timestamp = log.ActionDate,
                notes = log.Comments
            });
        }

        // Material receipt event
        if (po.MaterialReceipt != null)
        {
            timeline.Add(new
            {
                step = po.RequiredApprovalLevels + 3,
                stage = "Goods Received",
                status = "MaterialReceived",
                actor = po.MaterialReceipt.ReceivedBy ?? "Unknown",
                timestamp = po.MaterialReceipt.ReceivedDate,
                notes = po.MaterialReceipt.Notes
            });

            if (po.MaterialReceipt.VerifiedDate.HasValue)
            {
                timeline.Add(new
                {
                    step = po.RequiredApprovalLevels + 4,
                    stage = "Final Approval",
                    status = "FinalApproved",
                    actor = po.MaterialReceipt.VerifiedBy ?? "Unknown",
                    timestamp = po.MaterialReceipt.VerifiedDate,
                    notes = po.MaterialReceipt.VerificationNotes
                });
            }
        }

        // Payment event
        if (po.Payment != null)
        {
            timeline.Add(new
            {
                step = po.RequiredApprovalLevels + 5,
                stage = "Payment Processed",
                status = "Completed",
                actor = po.Payment.PaidBy ?? "Unknown",
                timestamp = po.Payment.PaymentDate,
                notes = $"{po.Payment.PaymentMethod} — {po.Payment.Notes}"
            });
        }

        return Ok(new
        {
            poId = po.Id,
            poNumber = po.PONumber,
            currentStatus = po.Status.ToString(),
            timeline
        });
    }

    /// <summary>
    /// GET /api/workflow/all-statuses
    /// Returns a list of all POs with their current workflow stage (for dashboard use).
    /// </summary>
    [HttpGet("all-statuses")]
    public async Task<IActionResult> GetAllStatuses()
    {
        var pos = await _db.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

       var result = pos.Select(po => new
{
    po.Id,
    po.PONumber,
    po.VendorName,
    po.TotalAmount,
    CurrentStatus = po.Status.ToString(),
    StageLabel = _workflow.GetStageLabel(po.Status),
    NextAction = _workflow.GetNextAction(po.Status),
    po.RequiredApprovalLevels,
    po.CurrentApprovalLevel,
    po.CreatedAt,
    po.UpdatedAt
});

        return Ok(result);
    }

    /// <summary>
    /// GET /api/workflow/transition-check?from=Approved&to=SentToVendor
    /// Returns whether a given transition is valid.
    /// </summary>
    [HttpGet("transition-check")]
    public IActionResult CheckTransition([FromQuery] string from, [FromQuery] string to)
    {
        if (!Enum.TryParse<POStatus>(from, out var fromStatus))
            return BadRequest($"Unknown status: {from}");
        if (!Enum.TryParse<POStatus>(to, out var toStatus))
            return BadRequest($"Unknown status: {to}");

        bool allowed = _workflow.CanTransition(fromStatus, toStatus);
        return Ok(new { from, to, allowed });
    }
}
