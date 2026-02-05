using System.Text;
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
    private const string LocationPrefix = "[Location: ";

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

        // Get recent booth locations from past sales
        var recentLocations = await _dbContext.Orders
            .Where(o => o.OrderType == "Booth Sale" && o.Notes != null && o.Notes.StartsWith(LocationPrefix))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => o.Notes!)
            .Take(50)
            .ToListAsync();

        var uniqueLocations = recentLocations
            .Select(n => ExtractLocation(n))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .Take(10)
            .ToList();

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
            },
            RecentLocations = uniqueLocations
        };

        return View(vm);
    }

    private static string? ExtractLocation(string? notes)
    {
        if (string.IsNullOrEmpty(notes) || !notes.StartsWith(LocationPrefix)) return null;
        var endIdx = notes.IndexOf(']', LocationPrefix.Length);
        if (endIdx < 0) return null;
        return notes.Substring(LocationPrefix.Length, endIdx - LocationPrefix.Length);
    }

    // ───────── EXPORT CSV ─────────
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? date)
    {
        var targetDate = string.IsNullOrEmpty(date)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(date).Date;

        var orders = await _dbContext.Orders
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt.Date == targetDate)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Sale #,Time,Location,Products,Payment Method,Boxes,Total,Notes");

        var counter = 1;
        foreach (var order in orders)
        {
            var location = ExtractLocation(order.Notes) ?? "";
            var notes = order.Notes ?? "";
            if (notes.StartsWith(LocationPrefix))
            {
                var endIdx = notes.IndexOf(']');
                if (endIdx >= 0 && endIdx + 1 < notes.Length)
                    notes = notes.Substring(endIdx + 1).Trim();
                else
                    notes = "";
            }

            var products = string.Join("; ", order.LineItems.Select(li => $"{li.Product?.Name} x{li.QuantityBoxes}"));

            sb.AppendLine($"{counter},{order.CreatedAt:HH:mm},\"{EscapeCsv(location)}\",\"{EscapeCsv(products)}\",{order.PaymentMethod},{order.TotalQty},{order.TotalPrice:F2},\"{EscapeCsv(notes)}\"");
            counter++;
        }

        // Summary row
        sb.AppendLine();
        sb.AppendLine($"TOTALS,,,,,{orders.Sum(o => o.TotalQty ?? 0)},{orders.Sum(o => o.TotalPrice ?? 0):F2},");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"booth-sales-{targetDate:yyyy-MM-dd}.csv");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
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

        // Format notes: prepend location if provided
        var notes = "";
        if (!string.IsNullOrWhiteSpace(req.Location))
            notes = $"{LocationPrefix}{req.Location.Trim()}] ";
        if (!string.IsNullOrWhiteSpace(req.Notes))
            notes += req.Notes.Trim();
        notes = notes.Trim();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderType = "Booth Sale",
            Status = "Completed",
            PaymentMethod = req.PaymentMethod ?? "Cash",
            IsOnlinePaid = false,
            OrderedAt = targetDate,
            Notes = string.IsNullOrEmpty(notes) ? null : notes,
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
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public List<BoothSaleItem> Items { get; set; } = new();
}

public class BoothSaleItem
{
    public Guid ProductId { get; set; }
    public int Qty { get; set; }
}
