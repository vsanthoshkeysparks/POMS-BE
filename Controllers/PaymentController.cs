using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Models;
using POManagement.API.Services;

namespace POManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly WorkflowService _workflow;

    public PaymentController(AppDbContext db, WorkflowService workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    // GET: api/payment/pending - FinalApproved POs waiting for payment
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _db.PurchaseOrders
            .Where(po => po.Status == POStatus.FinalApproved)
            .OrderBy(po => po.UpdatedAt)
            .Select(po => new
            {
                po.Id,
                po.PONumber,
                po.VendorName,
                po.TotalAmount,
                DueDate = po.PODate.AddDays(30),
                Status = po.Status.ToString()
            })
            .ToListAsync();

        return Ok(pending);
    }

    // POST: api/payment/{id}/pay
    [HttpPost("{id}/pay")]
    public async Task<IActionResult> ProcessPayment(int id, [FromBody] ProcessPaymentRequest request)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Payment)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();
        if (po.Status != POStatus.FinalApproved)
            return BadRequest("Only FinalApproved POs can be processed for payment.");

        // Guard: enforce workflow transition
        if (!_workflow.CanTransition(po.Status, POStatus.PaymentProcessing))
            return BadRequest("Invalid workflow transition. PO must be in FinalApproved status.");

        var payment = new Payment
        {
            PurchaseOrderId = po.Id,
            PaymentDate = DateTime.UtcNow,
            Amount = po.TotalAmount,
            PaidBy = request.PaidBy,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes
        };

        // Transition through PaymentProcessing and immediately to Completed
        po.Status = POStatus.PaymentProcessing;
        po.UpdatedAt = DateTime.UtcNow;
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // Final state: Completed
        po.Status = POStatus.Completed;
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Payment processed successfully. PO is now Completed.", po.PONumber, amount = po.TotalAmount });
    }
}

public class ProcessPaymentRequest
{
    public string? PaidBy { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}
