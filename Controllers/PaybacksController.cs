using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class PaybacksController : Controller
{
    private readonly AppDbContext _dbContext;

    public PaybacksController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var paybacks = await _dbContext.Paybacks
            .Include(payback => payback.Order)
            .Include(payback => payback.Customer)
            .OrderByDescending(payback => payback.PaidAt)
            .ToListAsync();

        return View(paybacks);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new PaybackCreateViewModel
        {
            Orders = await BuildOrderOptionsAsync(),
            Customers = await BuildCustomerOptionsAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaybackCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Orders = await BuildOrderOptionsAsync();
            model.Customers = await BuildCustomerOptionsAsync();
            return View(model);
        }

        var payback = new Payback
        {
            Id = Guid.NewGuid(),
            OrderId = model.OrderId,
            CustomerId = model.CustomerId,
            PaidAt = model.PaidAt,
            Amount = model.Amount,
            Method = model.Method,
            Notes = model.Notes
        };

        _dbContext.Paybacks.Add(payback);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> BuildOrderOptionsAsync()
    {
        return await _dbContext.Orders
            .Include(order => order.Customer)
            .OrderByDescending(order => order.OrderedAt)
            .Select(order => new SelectListItem(
                $"{order.OrderedAt:yyyy-MM-dd} - {order.Customer!.Name}",
                order.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> BuildCustomerOptionsAsync()
    {
        var customers = await _dbContext.Customers
            .OrderBy(customer => customer.Name)
            .Select(customer => new SelectListItem(customer.Name, customer.Id.ToString()))
            .ToListAsync();

        customers.Insert(0, new SelectListItem("Unassigned", ""));
        return customers;
    }
}
