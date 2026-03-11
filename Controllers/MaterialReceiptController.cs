using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Models;
using POManagement.API.Services;

namespace POManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MaterialReceiptController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkflowService _workflow;

    public MaterialReceiptController(AppDbContext db, WorkflowService workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    // GET: api/materialreceipt/pending - MaterialDispatched POs waiting for goods receipt
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _db.PurchaseOrders
            .Where(po => po.Status == POStatus.MaterialDispatched)
            .OrderBy(po => po.UpdatedAt)
            .Select(po => new
            {
                po.Id,
                po.PONumber,
                po.VendorName,
                po.PurchaseType,
                po.PODate,
                po.TotalAmount,
                ExpectedDate = po.PODate.AddDays(7),
                Status = "In Transit"
            })
            .ToListAsync();

        return Ok(pending);
    }

    // GET: api/materialreceipt/verification-pending - POs awaiting final verification
    [HttpGet("verification-pending")]
    public async Task<IActionResult> GetVerificationPending()
    {
        var pending = await _db.PurchaseOrders
            .Include(po => po.MaterialReceipt)
            .Where(po => po.Status == POStatus.VerificationPending)
            .OrderBy(po => po.UpdatedAt)
            .Select(po => new
            {
                po.Id,
                po.PONumber,
                po.VendorName,
                po.PurchaseType,
                po.PODate,
                po.TotalAmount,
                ReceivedDate = po.MaterialReceipt != null ? po.MaterialReceipt.ReceivedDate : (DateTime?)null,
                ReceivedBy = po.MaterialReceipt != null ? po.MaterialReceipt.ReceivedBy : null
            })
            .ToListAsync();

        return Ok(pending);
    }

    // POST: api/materialreceipt/{id}/receive
    [HttpPost("{id}/receive")]
    public async Task<IActionResult> ReceiveGoods(int id, [FromBody] ReceiveGoodsRequest request)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.MaterialReceipt)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();
        if (po.Status != POStatus.MaterialDispatched)
            return BadRequest("Only MaterialDispatched POs can have goods received.");

        // Guard: enforce workflow transition
        if (!_workflow.CanTransition(po.Status, POStatus.VerificationPending))
            return BadRequest("Invalid workflow transition. PO must be in MaterialDispatched status.");

        var receipt = new MaterialReceipt
        {
            PurchaseOrderId = po.Id,
            ReceivedDate = DateTime.UtcNow,
            ReceivedBy = request.ReceivedBy,
            Notes = request.Notes
        };

        // Advance to VerificationPending (requires final approval before payment)
        po.Status = POStatus.VerificationPending;
        po.UpdatedAt = DateTime.UtcNow;
        _db.MaterialReceipts.Add(receipt);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Goods received. Pending final verification before payment.", po.PONumber });
    }

    // POST: api/materialreceipt/{id}/final-approval
    [HttpPost("{id}/final-approval")]
    public async Task<IActionResult> FinalApproval(int id, [FromBody] FinalApprovalRequest request)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.MaterialReceipt)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();
        if (po.Status != POStatus.VerificationPending)
            return BadRequest("Only VerificationPending POs can receive final approval.");

        // Guard: enforce workflow transition
        if (!_workflow.CanTransition(po.Status, POStatus.FinalApproved))
            return BadRequest("Invalid workflow transition. PO must be in VerificationPending status.");

        if (po.MaterialReceipt != null)
        {
            po.MaterialReceipt.VerifiedBy = request.VerifiedBy;
            po.MaterialReceipt.VerifiedDate = DateTime.UtcNow;
            po.MaterialReceipt.VerificationNotes = request.Notes;
        }

        po.Status = POStatus.FinalApproved;
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Final approval granted. PO is ready for payment.", po.PONumber });
    }
}

public class ReceiveGoodsRequest
{
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
}

public class FinalApprovalRequest
{
    public string? VerifiedBy { get; set; }
    public string? Notes { get; set; }
}
