using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POManagement.API.Data;
using POManagement.API.Models;
using POManagement.API.Services;

namespace POManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseOrdersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ApprovalRoutingService _approvalService;
    private readonly WorkflowService _workflow;

    public PurchaseOrdersController(AppDbContext db, ApprovalRoutingService approvalService, WorkflowService workflow)
    {
        _db = db;
        _approvalService = approvalService;
        _workflow = workflow;
    }

    // GET: api/purchaseorders
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _db.PurchaseOrders
            .Include(po => po.LineItems)
            .Include(po => po.ApprovalLogs)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();

        return Ok(orders);
    }

    // GET: api/purchaseorders/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var po = await _db.PurchaseOrders
            .Include(po => po.LineItems)
            .Include(po => po.ApprovalLogs)
            .Include(po => po.MaterialReceipt)
            .Include(po => po.Payment)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (po == null) return NotFound();

        return Ok(po);
    }

    // POST: api/purchaseorders
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePORequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        decimal subtotal = request.LineItems.Sum(li => li.Quantity * li.UnitPrice);

        var po = new PurchaseOrder
        {
            PONumber = await GeneratePONumberAsync(),
            VendorName = request.VendorName ?? string.Empty,
            PurchaseType = request.PurchaseType ?? string.Empty,
            PODate = request.PODate,
            Status = request.SubmitForApproval ? POStatus.PendingApprovalLevel1 : POStatus.Draft,
            TotalAmount = subtotal,
            RequiredApprovalLevels = _approvalService.GetRequiredApprovalLevels(subtotal),
            CurrentApprovalLevel = 0,
            CreatedBy = request.CreatedBy,
            Notes = request.Notes,
            LineItems = request.LineItems.Select(li => new LineItem
            {
                Description = li.Description ?? string.Empty,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice,
                Amount = Math.Round(li.Quantity * li.UnitPrice, 2)
            }).ToList()
        };

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = po.Id }, new
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
            po.CreatedBy,
            po.Notes,
            po.CreatedAt,
            LineItems = po.LineItems.Select(li => new
            {
                li.Id,
                li.Description,
                li.Quantity,
                li.UnitPrice,
                li.Amount
            })
        });
    }

    // PUT: api/purchaseorders/5
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreatePORequest request)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.LineItems)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (po == null) return NotFound();

        if (po.Status != POStatus.Draft)
            return BadRequest("Only draft POs can be edited.");

        decimal subtotal = request.LineItems.Sum(li => li.Quantity * li.UnitPrice);

        po.VendorName = request.VendorName ?? string.Empty;
        po.PurchaseType = request.PurchaseType ?? string.Empty;
        po.PODate = request.PODate;
        po.TotalAmount = subtotal;
        po.RequiredApprovalLevels = _approvalService.GetRequiredApprovalLevels(subtotal);
        po.Notes = request.Notes;
        po.UpdatedAt = DateTime.UtcNow;

        if (request.SubmitForApproval)
            po.Status = POStatus.PendingApprovalLevel1;

        _db.LineItems.RemoveRange(po.LineItems);

        po.LineItems = request.LineItems.Select(li => new LineItem
        {
            Description = li.Description ?? string.Empty,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice,
            Amount = Math.Round(li.Quantity * li.UnitPrice, 2)
        }).ToList();

        await _db.SaveChangesAsync();

        return Ok(new
        {
            po.Id,
            po.PONumber,
            po.VendorName,
            po.PurchaseType,
            po.TotalAmount,
            Status = po.Status.ToString(),
            po.RequiredApprovalLevels
        });
    }

    // DELETE: api/purchaseorders/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);

        if (po == null)
            return NotFound();

        if (po.Status != POStatus.Draft)
            return BadRequest("Only draft POs can be deleted.");

        _db.PurchaseOrders.Remove(po);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // GET: api/purchaseorders/routing-info
    [HttpGet("routing-info")]
    public IActionResult GetRoutingInfo([FromQuery] decimal total)
    {
        return Ok(new
        {
            requiredLevels = _approvalService.GetRequiredApprovalLevels(total),
            label = _approvalService.GetApprovalRoutingLabel(total)
        });
    }

    // GET: api/purchaseorders/approved
    [HttpGet("approved")]
    public async Task<IActionResult> GetApproved()
    {
        var approved = await _db.PurchaseOrders
            .Where(po => po.Status == POStatus.Approved)
            .OrderBy(po => po.UpdatedAt)
            .Select(po => new
            {
                po.Id,
                po.PONumber,
                po.VendorName,
                po.PurchaseType,
                po.PODate,
                po.TotalAmount,
                Status = po.Status.ToString()
            })
            .ToListAsync();

        return Ok(approved);
    }

    // GET: api/purchaseorders/sent-to-vendor
    [HttpGet("sent-to-vendor")]
    public async Task<IActionResult> GetSentToVendor()
    {
        var sent = await _db.PurchaseOrders
            .Where(po => po.Status == POStatus.SentToVendor)
            .OrderBy(po => po.UpdatedAt)
            .Select(po => new
            {
                po.Id,
                po.PONumber,
                po.VendorName,
                po.PurchaseType,
                po.PODate,
                po.TotalAmount,
                Status = po.Status.ToString()
            })
            .ToListAsync();

        return Ok(sent);
    }

    // POST: api/purchaseorders/{id}/send-to-vendor
    [HttpPost("{id}/send-to-vendor")]
    public async Task<IActionResult> SendToVendor(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);

        if (po == null)
            return NotFound();

        if (po.Status != POStatus.Approved)
            return BadRequest("Only fully Approved POs can be sent to vendor.");

        po.Status = POStatus.SentToVendor;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = $"PO {po.PONumber} has been sent to vendor." });
    }

    // POST: api/purchaseorders/{id}/material-dispatched
    [HttpPost("{id}/material-dispatched")]
    public async Task<IActionResult> MarkMaterialDispatched(int id)
    {
        var po = await _db.PurchaseOrders.FindAsync(id);

        if (po == null)
            return NotFound();

        if (po.Status != POStatus.SentToVendor)
            return BadRequest("Only SentToVendor POs can be marked as dispatched.");

        po.Status = POStatus.MaterialDispatched;
        po.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = $"Material dispatched confirmed for PO {po.PONumber}." });
    }

    private async Task<string> GeneratePONumberAsync()
    {
        var year = DateTime.UtcNow.Year;

        int count = await _db.PurchaseOrders
            .CountAsync(p => p.PONumber.StartsWith($"PO-{year}-"));

        return $"PO-{year}-{(count + 1):D3}";
    }
}


// DTOs

public class CreatePORequest
{
    public string? VendorName { get; set; }
    public string? PurchaseType { get; set; }
    public DateTime PODate { get; set; }
    public bool SubmitForApproval { get; set; }
    public string? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public List<LineItemRequest> LineItems { get; set; } = new();
}

public class LineItemRequest
{
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}