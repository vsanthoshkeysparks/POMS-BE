using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Models;
using POManagement.API.Services;

namespace POManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ApprovalsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ApprovalRoutingService _approvalService;
    private readonly WorkflowService _workflow;

    public ApprovalsController(AppDbContext db, ApprovalRoutingService approvalService, WorkflowService workflow)
    {
        _db = db;
        _approvalService = approvalService;
        _workflow = workflow;
    }

    // GET: api/approvals/pending
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var raw = await _db.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.Status == POStatus.PendingApprovalLevel1 || po.Status == POStatus.PendingApprovalLevel2)
            .OrderBy(po => po.CreatedAt)
            .ToListAsync();

        var result = raw.Select(po => new
        {
            po.Id,
            po.PONumber,
            po.VendorName,
            po.PurchaseType,
            po.PODate,
            po.TotalAmount,
            Status = po.Status.ToString(),
            po.RequiredApprovalLevels,
            po.CurrentApprovalLevel,
            RoutingLabel = po.TotalAmount > 10000m
                ? "2-Level Approval (Manager + Director)"
                : "1-Level Approval (Manager)",
            IsUrgent = po.TotalAmount > 10000m
        });

        return Ok(result);
    }

    // GET: api/approvals/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var pendingCount = await _db.PurchaseOrders.CountAsync(
            po => po.Status == POStatus.PendingApprovalLevel1 || po.Status == POStatus.PendingApprovalLevel2);

        var urgentCount = await _db.PurchaseOrders.CountAsync(
            po => (po.Status == POStatus.PendingApprovalLevel1 || po.Status == POStatus.PendingApprovalLevel2)
                  && po.TotalAmount > 10000m);

        return Ok(new { pendingCount, urgentCount });
    }

    // POST: api/approvals/{id}/action
    [HttpPost("{id}/action")]
    public async Task<IActionResult> ProcessAction(int id, [FromBody] ApprovalActionRequest request)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.ApprovalLogs)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null)
            return NotFound();

        if (po.Status != POStatus.PendingApprovalLevel1 && po.Status != POStatus.PendingApprovalLevel2)
            return BadRequest("This PO is not pending approval.");

        var currentLevel = po.CurrentApprovalLevel + 1;

        var (newStatus, message) = _approvalService.ProcessApproval(
            po,
            request.Action,
            request.ApproverName,
            request.Comments);

        if (!_workflow.CanTransition(po.Status, newStatus))
            return BadRequest($"Invalid workflow transition from '{po.Status}' to '{newStatus}'.");

        var log = new ApprovalLog
        {
            PurchaseOrderId = po.Id,
            ApprovalLevel = currentLevel,
            ApproverName = request.ApproverName,
            Action = request.Action,
            Comments = request.Comments,
            ActionDate = DateTime.UtcNow
        };

        po.ApprovalLogs.Add(log);
        po.Status = newStatus;
        po.CurrentApprovalLevel = currentLevel;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message,
            newStatus = newStatus.ToString(),
            po.PONumber
        });
    }
}

public class ApprovalActionRequest
{
    public string Action { get; set; } = string.Empty;
    public string? ApproverName { get; set; }
    public string? Comments { get; set; }
}