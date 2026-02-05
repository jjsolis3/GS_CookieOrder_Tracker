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

    // ───────── MAIN PAGE ─────────
    public async Task<IActionResult> Index(string? date)
    {
        var targetDate = string.IsNullOrEmpty(date)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(date).Date;

        // Find active booth session (not ended)
        var activeSession = await _dbContext.BoothSessions
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        // If there's an active session, show sales for that session
        // Otherwise show sales for the target date
        List<Order> orders;
        if (activeSession != null)
        {
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Where(o => o.BoothSessionId == activeSession.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        else
        {
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt.Date == targetDate)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // Products with images for card grid
        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var productCards = products.Select(p => new ProductCard
        {
            Id = p.Id,
            Name = p.Name,
            PricePerBox = p.PricePerBox,
            ImagePath = p.ImagePath
        }).ToList();

        // Recent locations for autocomplete
        var recentLocations = await _dbContext.BoothSessions
            .OrderByDescending(s => s.StartedAt)
            .Select(s => s.Location)
            .Take(20)
            .ToListAsync();

        var uniqueLocations = recentLocations
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct()
            .Take(10)
            .ToList();

        // Fallback: also check old-style location-in-notes
        if (!uniqueLocations.Any())
        {
            var noteLocations = await _dbContext.Orders
                .Where(o => o.OrderType == "Booth Sale" && o.Notes != null && o.Notes.StartsWith(LocationPrefix))
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => o.Notes!)
                .Take(50)
                .ToListAsync();

            uniqueLocations = noteLocations
                .Select(n => ExtractLocation(n))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct()
                .Take(10)
                .ToList()!;
        }

        var vm = new BoothingViewModel
        {
            SelectedDate = targetDate,
            Orders = orders,
            TotalSales = orders.Sum(o => o.TotalQty ?? 0),
            TotalRevenue = orders.Sum(o => o.TotalPrice ?? 0),
            TotalCollected = orders.Sum(o => o.PaidAmount ?? 0),
            ProductCards = productCards,
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
            RecentLocations = uniqueLocations,
            ActiveSession = activeSession != null ? new BoothSessionInfo
            {
                Id = activeSession.Id,
                Location = activeSession.Location,
                StartedAt = activeSession.StartedAt,
                ScoutCount = activeSession.ScoutCount,
                Notes = activeSession.Notes
            } : null
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

    // ───────── START BOOTH SESSION ─────────
    [HttpPost]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Location))
            return BadRequest(new { error = "Location is required." });

        // End any existing active sessions first
        var activeSessions = await _dbContext.BoothSessions
            .Where(s => s.EndedAt == null)
            .ToListAsync();
        foreach (var s in activeSessions)
        {
            s.EndedAt = DateTime.UtcNow;
            s.UpdatedAt = DateTime.UtcNow;
        }

        var session = new BoothSession
        {
            Id = Guid.NewGuid(),
            Location = req.Location.Trim(),
            StartedAt = DateTime.UtcNow,
            ScoutCount = req.ScoutCount > 0 ? req.ScoutCount : 1,
            Notes = req.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.BoothSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, sessionId = session.Id });
    }

    // ───────── END BOOTH SESSION ─────────
    [HttpPost]
    public async Task<IActionResult> EndSession([FromBody] EndSessionRequest req)
    {
        var session = await _dbContext.BoothSessions.FindAsync(req.SessionId);
        if (session == null) return NotFound(new { error = "Session not found." });

        session.EndedAt = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true });
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

        // Format notes: prepend location if provided (for non-session sales)
        var notes = "";
        if (!string.IsNullOrWhiteSpace(req.Location) && req.SessionId == null)
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
            BoothSessionId = req.SessionId,
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

    // ───────── GET SALE DETAIL (for edit modal) ─────────
    [HttpGet]
    public async Task<IActionResult> GetSaleDetail(Guid id)
    {
        var order = await _dbContext.Orders
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return Json(new
        {
            id = order.Id,
            paymentMethod = order.PaymentMethod,
            notes = order.Notes,
            lineItems = order.LineItems.Select(li => new
            {
                id = li.Id,
                productId = li.ProductId,
                productName = li.Product?.Name,
                quantityBoxes = li.QuantityBoxes,
                unitPrice = li.UnitPrice,
                lineTotal = li.QuantityBoxes * li.UnitPrice
            })
        });
    }

    // ───────── UPDATE SALE ─────────
    [HttpPost]
    public async Task<IActionResult> UpdateSale([FromBody] UpdateBoothSaleRequest req)
    {
        var order = await _dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId);

        if (order == null) return NotFound(new { error = "Sale not found." });

        // Update payment method and notes
        order.PaymentMethod = req.PaymentMethod ?? order.PaymentMethod;
        order.Notes = req.Notes;
        order.UpdatedAt = DateTime.UtcNow;

        if (req.Items != null && req.Items.Count > 0)
        {
            // Remove old line items
            _dbContext.OrderLineItems.RemoveRange(order.LineItems);

            var productIds = req.Items.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var totalQty = 0;
            decimal totalPrice = 0;

            foreach (var item in req.Items)
            {
                if (!products.ContainsKey(item.ProductId) || item.Qty <= 0) continue;
                var price = products[item.ProductId].PricePerBox;
                totalQty += item.Qty;
                totalPrice += item.Qty * price;

                _dbContext.OrderLineItems.Add(new OrderLineItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    QuantityBoxes = item.Qty,
                    UnitPrice = price,
                    InventorySource = "Troop",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            order.TotalQty = totalQty;
            order.TotalPrice = totalPrice;
            order.PaidAmount = totalPrice;
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── DELETE SALE ─────────
    [HttpPost]
    public async Task<IActionResult> DeleteSale([FromBody] DeleteBoothSaleRequest req)
    {
        var order = await _dbContext.Orders
            .Include(o => o.LineItems)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId);

        if (order == null) return NotFound(new { error = "Sale not found." });

        _dbContext.OrderLineItems.RemoveRange(order.LineItems);
        _dbContext.Orders.Remove(order);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // ───────── EXPORT CSV ─────────
    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? date, Guid? sessionId)
    {
        var targetDate = string.IsNullOrEmpty(date)
            ? DateTime.UtcNow.Date
            : DateTime.Parse(date).Date;

        List<Order> orders;
        string fileLabel;

        if (sessionId.HasValue)
        {
            var session = await _dbContext.BoothSessions.FindAsync(sessionId.Value);
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Where(o => o.BoothSessionId == sessionId.Value)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
            fileLabel = session != null
                ? $"booth-{session.Location.Replace(" ", "-")}-{session.StartedAt:yyyy-MM-dd}"
                : $"booth-session-{targetDate:yyyy-MM-dd}";
        }
        else
        {
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt.Date == targetDate)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
            fileLabel = $"booth-sales-{targetDate:yyyy-MM-dd}";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Sale #,Time,Products,Payment Method,Boxes,Total,Notes");

        var counter = 1;
        foreach (var order in orders)
        {
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

            sb.AppendLine($"{counter},{order.CreatedAt:HH:mm},\"{EscapeCsv(products)}\",{order.PaymentMethod},{order.TotalQty},{order.TotalPrice:F2},\"{EscapeCsv(notes)}\"");
            counter++;
        }

        sb.AppendLine();
        sb.AppendLine($"TOTALS,,,,{orders.Sum(o => o.TotalQty ?? 0)},{orders.Sum(o => o.TotalPrice ?? 0):F2},");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{fileLabel}.csv");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\"", "\"\"");
    }
}

// ───────── Request DTOs ─────────
public class StartSessionRequest
{
    public string Location { get; set; } = "";
    public int ScoutCount { get; set; } = 1;
    public string? Notes { get; set; }
}

public class EndSessionRequest
{
    public Guid SessionId { get; set; }
}

public class QuickBoothSaleRequest
{
    public string? Date { get; set; }
    public Guid? SessionId { get; set; }
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

public class UpdateBoothSaleRequest
{
    public Guid OrderId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public List<BoothSaleItem>? Items { get; set; }
}

public class DeleteBoothSaleRequest
{
    public Guid OrderId { get; set; }
}
