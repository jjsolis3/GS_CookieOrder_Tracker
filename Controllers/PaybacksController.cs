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

    /// <summary>Calculated payback report: what is owed from non-online orders, minus what has been paid.</summary>
    public async Task<IActionResult> Index()
    {
        // Line items from orders that require payback (exclude Online Delivery -- already paid online)
        var owedByProduct = await _dbContext.OrderLineItems
            .Include(li => li.Order)
            .Include(li => li.Product)
            .Where(li => li.Order!.OrderType != "Online Delivery")
            .GroupBy(li => new { li.Product!.Name, li.Product.PricePerBox })
            .Select(g => new PaybackProductRow
            {
                ProductName = g.Key.Name,
                PricePerBox = g.Key.PricePerBox,
                BoxesSold = g.Sum(li => li.QuantityBoxes)
            })
            .OrderBy(r => r.ProductName)
            .ToListAsync();

        var owedFromSales = owedByProduct.Sum(r => r.AmountOwed);

        // Subtract the value of returned product (returns remove payback responsibility)
        var returnedValue = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .SumAsync(r => (decimal?)((r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase) * r.Product.PricePerBox)) ?? 0m;

        var totalOwed = owedFromSales - returnedValue;
        if (totalOwed < 0) totalOwed = 0;

        var totalPaid = await _dbContext.Paybacks.SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var recentPayments = await _dbContext.Paybacks
            .Include(p => p.Order).ThenInclude(o => o!.Customer)
            .OrderByDescending(p => p.PaidAt)
            .Take(20)
            .Select(p => new PaybackPaymentRow
            {
                PaidAt = p.PaidAt,
                Amount = p.Amount,
                Method = p.Method,
                Notes = p.Notes,
                OrderInfo = p.Order != null
                    ? p.Order.OrderedAt.ToString("yyyy-MM-dd") + " - " + (p.Order.Customer != null ? p.Order.Customer.Name : "")
                    : ""
            })
            .ToListAsync();

        var vm = new PaybackSummaryViewModel
        {
            ByProduct = owedByProduct,
            TotalOwedFromSales = owedFromSales,
            TotalReturnedValue = returnedValue,
            TotalOwed = totalOwed,
            TotalPaid = totalPaid,
            RecentPayments = recentPayments
        };

        return View(vm);
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
            .Include(o => o.Customer)
            .Where(o => o.OrderType != "Online Delivery")
            .OrderByDescending(o => o.OrderedAt)
            .Select(o => new SelectListItem(
                $"{o.OrderedAt:yyyy-MM-dd} - {o.Customer!.Name} ({o.OrderType})",
                o.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> BuildCustomerOptionsAsync()
    {
        var customers = await _dbContext.Customers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        customers.Insert(0, new SelectListItem("Unassigned", ""));
        return customers;
    }
}
