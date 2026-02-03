using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _dbContext;

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new OrderCreateViewModel
        {
            LineItems = new List<OrderLineItemViewModel> { new() }
        };
        await PopulateDropdowns(viewModel);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateViewModel model)
    {
        // Remove items the user left blank (no product selected)
        model.LineItems.RemoveAll(li => li.ProductId == Guid.Empty);

        if (model.LineItems.Count == 0)
        {
            ModelState.AddModelError("", "At least one product line item is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model);
            return View(model);
        }

        var isOnlinePaid = model.OrderType == "Online Delivery";
        var totalPrice = model.LineItems.Sum(li => li.QuantityBoxes * li.UnitPrice);

        // Ensure the OrderedAt value is treated as UTC for PostgreSQL timestamptz
        var orderedUtc = DateTime.SpecifyKind(model.OrderedAt, DateTimeKind.Utc);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = model.CustomerId,
            GirlScoutId = model.GirlScoutId,
            OrderType = model.OrderType,
            PaymentMethod = model.PaymentMethod,
            IsOnlinePaid = isOnlinePaid,
            Status = isOnlinePaid ? "Paid" : "Pending",
            OrderedAt = orderedUtc,
            DeliveryDate = model.DeliveryDate,
            Notes = model.Notes,
            TotalQty = model.LineItems.Sum(li => li.QuantityBoxes),
            TotalPrice = totalPrice,
            PaidAmount = isOnlinePaid ? totalPrice : 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);

        foreach (var li in model.LineItems)
        {
            // Default inventory source based on order type
            var source = model.OrderType == "Booth Sale" ? "Troop" : li.InventorySource;

            var lineItem = new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = li.ProductId,
                QuantityBoxes = li.QuantityBoxes,
                UnitPrice = li.UnitPrice,
                InventorySource = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.OrderLineItems.Add(lineItem);
        }

        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDropdowns(OrderCreateViewModel model)
    {
        model.Customers = await BuildCustomerOptionsAsync();
        model.GirlScouts = await BuildGirlScoutOptionsAsync();
        model.Products = await BuildProductOptionsAsync();
        model.OrderTypes = new List<SelectListItem>
        {
            new("Direct Sale", "Direct Sale"),
            new("Online Delivery", "Online Delivery"),
            new("Booth Sale", "Booth Sale")
        };
        model.PaymentMethods = new List<SelectListItem>
        {
            new("Cash", "Cash"),
            new("Credit Card", "Credit Card"),
            new("Venmo", "Venmo"),
            new("Zelle", "Zelle"),
            new("Check", "Check"),
            new("Online Payment", "Online Payment"),
            new("Other", "Other")
        };
        model.InventorySources = new List<SelectListItem>
        {
            new("Personal", "Personal"),
            new("Troop", "Troop")
        };
    }

    private async Task<List<SelectListItem>> BuildCustomerOptionsAsync()
    {
        var customers = await _dbContext.Customers
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        // If a "Walk-up Customer" or generic row exists it will sort naturally.
        // Otherwise, add a placeholder.
        if (!customers.Any(c => c.Text.Contains("Walk-up", StringComparison.OrdinalIgnoreCase)
                              || c.Text.Contains("Generic", StringComparison.OrdinalIgnoreCase)))
        {
            customers.Insert(0, new SelectListItem("-- select customer --", ""));
        }

        return customers;
    }

    private async Task<List<SelectListItem>> BuildGirlScoutOptionsAsync()
    {
        var scouts = await _dbContext.GirlScouts
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new SelectListItem($"{s.FirstName} {s.LastName}", s.Id.ToString()))
            .ToListAsync();

        scouts.Insert(0, new SelectListItem("Unassigned", ""));
        return scouts;
    }

    private async Task<List<SelectListItem>> BuildProductOptionsAsync()
    {
        return await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem($"{p.Name} (${p.PricePerBox})", p.Id.ToString()))
            .ToListAsync();
    }
}
