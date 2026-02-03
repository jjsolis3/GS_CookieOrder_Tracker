using GS_CookieOrder_Tracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly AppDbContext _dbContext;

    public CustomersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var customers = await _dbContext.Customers
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(customers);
    }

    // AJAX: Quick-create customer from modal (returns JSON)
    [HttpPost]
    public async Task<IActionResult> QuickCreate([FromBody] QuickCreateCustomerRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { error = "Customer name is required." });

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = req.Name.Trim(),
            Email = req.Email?.Trim(),
            Phone = req.Phone?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, id = customer.Id, name = customer.Name });
    }
}

public class QuickCreateCustomerRequest
{
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
