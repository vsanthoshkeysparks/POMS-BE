using Microsoft.AspNetCore.Mvc;

namespace POManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private static readonly List<object> Vendors = new()
    {
        new { id = 1, name = "Global Tech Solutions", category = "Technology" },
        new { id = 2, name = "Prime Office Supplies", category = "Supplies" },
        new { id = 3, name = "Apex Industrial", category = "Industrial" },
        new { id = 4, name = "Metro Logistics", category = "Services" },
        new { id = 5, name = "Digital Systems Inc", category = "Technology" },
        new { id = 6, name = "BuildRight Contractors", category = "Construction" },
        new { id = 7, name = "CloudPeak Software", category = "Software" },
        new { id = 8, name = "SafeGuard Security", category = "Security" },
    };

    // GET: api/suppliers
    [HttpGet]
    public IActionResult GetAll() => Ok(Vendors);
}
