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

    // ───────── LIST ─────────
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

    // ───────── CREATE GET ─────────
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

    // ───────── CREATE POST ─────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateViewModel model)
    {
        model.LineItems.RemoveAll(li => li.ProductId == Guid.Empty);

        if (model.LineItems.Count == 0)
            ModelState.AddModelError("", "At least one product line item is required.");

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model);
            return View(model);
        }

        var isOnlinePaid = model.OrderType == "Online Delivery";
        var totalPrice = model.LineItems.Sum(li => li.QuantityBoxes * li.UnitPrice);
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
            var source = model.OrderType == "Booth Sale" ? "Troop" : li.InventorySource;
            _dbContext.OrderLineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = li.ProductId,
                QuantityBoxes = li.QuantityBoxes,
                UnitPrice = li.UnitPrice,
                InventorySource = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ───────── AJAX: Quick status update ─────────
    [HttpPost]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusRequest req)
    {
        var order = await _dbContext.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound();

        order.Status = req.Status;
        order.UpdatedAt = DateTime.UtcNow;

        if (req.Status == "Paid" || req.Status == "Completed")
            order.PaidAmount = order.TotalPrice;

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Get order detail for modal ─────────
    [HttpGet]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.PricePerBox })
            .ToListAsync();

        return Json(new
        {
            id = order.Id,
            orderType = order.OrderType,
            status = order.Status,
            paymentMethod = order.PaymentMethod,
            orderedAt = order.OrderedAt.ToString("yyyy-MM-dd"),
            deliveryDate = order.DeliveryDate?.ToString("yyyy-MM-dd"),
            notes = order.Notes,
            customerName = order.Customer?.Name,
            customerId = order.CustomerId,
            girlScoutName = order.GirlScout != null ? $"{order.GirlScout.FirstName} {order.GirlScout.LastName}" : "Unassigned",
            girlScoutId = order.GirlScoutId,
            totalPrice = order.TotalPrice,
            totalQty = order.TotalQty,
            paidAmount = order.PaidAmount,
            lineItems = order.LineItems.Select(li => new
            {
                id = li.Id,
                productId = li.ProductId,
                productName = li.Product?.Name,
                quantityBoxes = li.QuantityBoxes,
                unitPrice = li.UnitPrice,
                inventorySource = li.InventorySource,
                lineTotal = li.QuantityBoxes * li.UnitPrice
            }),
            products = products.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                price = p.PricePerBox
            })
        });
    }

    // ───────── AJAX: Update order header ─────────
    [HttpPost]
    public async Task<IActionResult> UpdateOrder([FromBody] UpdateOrderRequest req)
    {
        var order = await _dbContext.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound();

        if (order.Status == "Completed")
            return BadRequest(new { error = "Cannot edit a completed order. Change status first." });

        order.Status = req.Status ?? order.Status;
        order.PaymentMethod = req.PaymentMethod ?? order.PaymentMethod;
        order.Notes = req.Notes ?? order.Notes;
        order.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Add line item ─────────
    [HttpPost]
    public async Task<IActionResult> AddLineItem([FromBody] AddLineItemRequest req)
    {
        var order = await _dbContext.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound();
        if (order.Status == "Completed")
            return BadRequest(new { error = "Cannot edit a completed order." });

        var product = await _dbContext.Products.FindAsync(req.ProductId);
        if (product == null) return BadRequest(new { error = "Product not found." });

        var lineItem = new OrderLineItem
        {
            Id = Guid.NewGuid(),
            OrderId = req.OrderId,
            ProductId = req.ProductId,
            QuantityBoxes = req.QuantityBoxes,
            UnitPrice = product.PricePerBox,
            InventorySource = req.InventorySource ?? "Personal",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.OrderLineItems.Add(lineItem);

        // Recalculate totals
        await _dbContext.SaveChangesAsync();
        await RecalcOrderTotals(order.Id);

        return Ok(new { success = true, lineItemId = lineItem.Id });
    }

    // ───────── AJAX: Remove line item ─────────
    [HttpPost]
    public async Task<IActionResult> RemoveLineItem([FromBody] RemoveLineItemRequest req)
    {
        var li = await _dbContext.OrderLineItems.FindAsync(req.LineItemId);
        if (li == null) return NotFound();

        var order = await _dbContext.Orders.FindAsync(li.OrderId);
        if (order != null && order.Status == "Completed")
            return BadRequest(new { error = "Cannot edit a completed order." });

        _dbContext.OrderLineItems.Remove(li);
        await _dbContext.SaveChangesAsync();

        if (order != null)
            await RecalcOrderTotals(order.Id);

        return Ok(new { success = true });
    }

    // ───────── Recalculate order totals ─────────
    private async Task RecalcOrderTotals(Guid orderId)
    {
        var order = await _dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;

        order.TotalQty = order.LineItems.Sum(li => li.QuantityBoxes);
        order.TotalPrice = order.LineItems.Sum(li => li.QuantityBoxes * li.UnitPrice);
        order.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    // ───────── Dropdown helpers ─────────
    private async Task PopulateDropdowns(OrderCreateViewModel model)
    {
        model.Customers = await BuildCustomerOptionsAsync();
        model.GirlScouts = await BuildGirlScoutOptionsAsync();

        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.PricePerBox })
            .ToListAsync();

        model.Products = products
            .Select(p => new SelectListItem($"{p.Name} (${p.PricePerBox})", p.Id.ToString()))
            .ToList();

        model.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.PricePerBox);
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
}

// ───────── Request DTOs ─────────
public class UpdateStatusRequest
{
    public Guid OrderId { get; set; }
    public string Status { get; set; } = "";
}

public class UpdateOrderRequest
{
    public Guid OrderId { get; set; }
    public string? Status { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class AddLineItemRequest
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int QuantityBoxes { get; set; } = 1;
    public string? InventorySource { get; set; }
}

public class RemoveLineItemRequest
{
    public Guid LineItemId { get; set; }
}
