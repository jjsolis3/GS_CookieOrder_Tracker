using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class BoothingController : Controller
{
    private readonly AppDbContext _dbContext;

    public BoothingController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ───────── TODAY'S BOOTH SALES ─────────
    public async Task<IActionResult> Index(string? date)
    {
        var targetDate = string.IsNullOrEmpty(date)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(date).Date;

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt.Date == targetDate)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.PricePerBox })
            .ToListAsync();

        var vm = new BoothingViewModel
        {
            SelectedDate = targetDate,
            Orders = orders,
            TotalSales = orders.Sum(o => o.TotalQty ?? 0),
            TotalRevenue = orders.Sum(o => o.TotalPrice ?? 0),
            TotalCollected = orders.Sum(o => o.PaidAmount ?? 0),
            Products = products.Select(p => new SelectListItem($"{p.Name} (${p.PricePerBox})", p.Id.ToString())).ToList(),
            ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.PricePerBox),
            PaymentMethods = new List<SelectListItem>
            {
                new("Cash", "Cash"),
                new("Credit Card", "Credit Card"),
                new("Venmo", "Venmo"),
                new("Zelle", "Zelle"),
                new("Other", "Other")
            }
        };

        return View(vm);
    }

    // ───────── QUICK ADD BOOTH SALE ─────────
    [HttpPost]
    public async Task<IActionResult> QuickAdd([FromBody] QuickBoothSaleRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "At least one product is required." });

        var targetDate = DateTime.SpecifyKind(
            string.IsNullOrEmpty(req.Date) ? DateTime.UtcNow.Date : DateTime.Parse(req.Date).Date,
            DateTimeKind.Utc);

        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var totalQty = req.Items.Sum(i => i.Qty);
        var totalPrice = req.Items.Sum(i => i.Qty * (products.ContainsKey(i.ProductId) ? products[i.ProductId].PricePerBox : 0));

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderType = "Booth Sale",
            Status = "Completed",
            PaymentMethod = req.PaymentMethod ?? "Cash",
            IsOnlinePaid = false,
            OrderedAt = targetDate,
            Notes = req.Notes,
            TotalQty = totalQty,
            TotalPrice = totalPrice,
            PaidAmount = totalPrice,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);

        foreach (var item in req.Items)
        {
            if (!products.ContainsKey(item.ProductId)) continue;
            _dbContext.OrderLineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                QuantityBoxes = item.Qty,
                UnitPrice = products[item.ProductId].PricePerBox,
                InventorySource = "Troop",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true, orderId = order.Id });
    }
}

public class QuickBoothSaleRequest
{
    public string? Date { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public List<BoothSaleItem> Items { get; set; } = new();
}

public class BoothSaleItem
{
    public Guid ProductId { get; set; }
    public int Qty { get; set; }
}
