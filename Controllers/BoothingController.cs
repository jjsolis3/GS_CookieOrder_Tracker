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
        // Parse date and ensure it's UTC (PostgreSQL requires UTC for timestamp with time zone)
        DateTime targetDate;
        if (string.IsNullOrEmpty(date))
        {
            targetDate = DateTime.UtcNow.Date;
        }
        else
        {
            var parsedDate = DateTime.Parse(date);
            targetDate = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
        }

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
                .Include(o => o.GirlScout)
                .Where(o => o.BoothSessionId == activeSession.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        else
        {
            // Use date range comparison for UTC compatibility
            var startOfDay = targetDate;
            var endOfDay = targetDate.AddDays(1);
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Include(o => o.GirlScout)
                .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt >= startOfDay && o.OrderedAt < endOfDay)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // Products with images for card grid (ordered by SortOrder, then Name)
        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var productCards = products.Select(p => new ProductCard
        {
            Id = p.Id,
            Name = p.Name,
            PricePerBox = p.PricePerBox,
            ImagePath = p.ImagePath
        }).ToList();

        // Girl Scouts for per-sale attribution
        var scouts = await _dbContext.GirlScouts
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new SelectListItem($"{s.FirstName} {s.LastName}", s.Id.ToString()))
            .ToListAsync();

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
            Scouts = scouts,
            RecentLocations = uniqueLocations,
            ActiveSession = activeSession != null ? new BoothSessionInfo
            {
                Id = activeSession.Id,
                Location = activeSession.Location,
                StartedAt = activeSession.StartedAt,
                ScoutCount = activeSession.ScoutCount,
                Notes = activeSession.Notes
            } : null,
            // Include booth inventory if there's an active session
            BoothInventoryItems = activeSession != null
                ? await _dbContext.BoothInventories
                    .Include(bi => bi.Product)
                    .Where(bi => bi.BoothSessionId == activeSession.Id)
                    .Select(bi => new BoothInventoryItem
                    {
                        ProductId = bi.ProductId,
                        ProductName = bi.Product!.Name,
                        StartingQuantity = bi.StartingQuantity,
                        SoldQuantity = bi.SoldQuantity,
                        RemainingQuantity = bi.StartingQuantity - bi.SoldQuantity
                    })
                    .OrderBy(bi => bi.ProductName)
                    .ToListAsync()
                : new List<BoothInventoryItem>()
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

        // Add booth starting inventory if provided
        if (req.Inventory != null && req.Inventory.Count > 0)
        {
            foreach (var item in req.Inventory.Where(i => i.Quantity > 0))
            {
                _dbContext.BoothInventories.Add(new BoothInventory
                {
                    Id = Guid.NewGuid(),
                    BoothSessionId = session.Id,
                    ProductId = item.ProductId,
                    StartingQuantity = item.Quantity,
                    SoldQuantity = 0,
                    ReturnedQuantity = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        // Return inventory status for the new session
        var inventoryStatus = req.Inventory?
            .Where(i => i.Quantity > 0)
            .Select(i => new { productId = i.ProductId, starting = i.Quantity, sold = 0, remaining = i.Quantity })
            .ToList() ?? new List<object>();

        return Ok(new { success = true, sessionId = session.Id, inventory = inventoryStatus });
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

        // Resolve scout name if provided
        string? scoutName = null;
        if (req.GirlScoutId.HasValue)
        {
            var scout = await _dbContext.GirlScouts.FindAsync(req.GirlScoutId.Value);
            if (scout != null) scoutName = $"{scout.FirstName} {scout.LastName}";
        }

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
            GirlScoutId = req.GirlScoutId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);

        // If this is a session sale, load booth inventory to update sold quantities
        Dictionary<Guid, BoothInventory>? boothInventory = null;
        if (req.SessionId.HasValue)
        {
            boothInventory = await _dbContext.BoothInventories
                .Where(bi => bi.BoothSessionId == req.SessionId.Value)
                .ToDictionaryAsync(bi => bi.ProductId);
        }

        var lineItemDetails = new List<object>();
        var inventoryWarnings = new List<string>();

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

            // Update booth inventory sold quantity
            if (boothInventory != null && boothInventory.TryGetValue(item.ProductId, out var bi))
            {
                bi.SoldQuantity += item.Qty;
                bi.UpdatedAt = DateTime.UtcNow;

                // Check if oversold
                if (bi.SoldQuantity > bi.StartingQuantity)
                {
                    inventoryWarnings.Add($"{products[item.ProductId].Name}: sold {bi.SoldQuantity} but only had {bi.StartingQuantity}");
                }
            }

            lineItemDetails.Add(new
            {
                productName = products[item.ProductId].Name,
                qty = item.Qty
            });
        }

        await _dbContext.SaveChangesAsync();

        // Return updated session totals for real-time counter
        int sessionSaleCount = 0, sessionTotalBoxes = 0;
        decimal sessionTotalRevenue = 0;
        if (req.SessionId.HasValue)
        {
            var sessionOrders = await _dbContext.Orders
                .Where(o => o.BoothSessionId == req.SessionId.Value)
                .ToListAsync();
            sessionSaleCount = sessionOrders.Count;
            sessionTotalBoxes = sessionOrders.Sum(o => o.TotalQty ?? 0);
            sessionTotalRevenue = sessionOrders.Sum(o => o.TotalPrice ?? 0);
        }

        // Get updated booth inventory for response
        List<object>? updatedInventory = null;
        if (req.SessionId.HasValue)
        {
            updatedInventory = await _dbContext.BoothInventories
                .Include(bi => bi.Product)
                .Where(bi => bi.BoothSessionId == req.SessionId.Value)
                .Select(bi => new
                {
                    productId = bi.ProductId,
                    productName = bi.Product!.Name,
                    starting = bi.StartingQuantity,
                    sold = bi.SoldQuantity,
                    remaining = bi.StartingQuantity - bi.SoldQuantity
                })
                .ToListAsync<object>();
        }

        return Ok(new
        {
            success = true,
            orderId = order.Id,
            // Sale details for adding row to table
            sale = new
            {
                id = order.Id,
                time = order.CreatedAt.ToString("h:mm tt"),
                products = string.Join(", ", lineItemDetails.Select(li =>
                {
                    var d = (dynamic)li;
                    return $"{d.productName} x{d.qty}";
                })),
                paymentMethod = order.PaymentMethod,
                totalQty = order.TotalQty,
                totalPrice = order.TotalPrice,
                scoutName = scoutName
            },
            // Updated session KPIs for real-time counter
            kpi = new
            {
                saleCount = sessionSaleCount,
                totalBoxes = sessionTotalBoxes,
                totalRevenue = sessionTotalRevenue
            },
            // Updated booth inventory
            inventory = updatedInventory,
            warnings = inventoryWarnings.Any() ? inventoryWarnings : null
        });
    }

    // ───────── GET SALE DETAIL (for edit modal) ─────────
    [HttpGet]
    public async Task<IActionResult> GetSaleDetail(Guid id)
    {
        var order = await _dbContext.Orders
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Include(o => o.GirlScout)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return Json(new
        {
            id = order.Id,
            paymentMethod = order.PaymentMethod,
            notes = order.Notes,
            girlScoutId = order.GirlScoutId,
            scoutName = order.GirlScout != null ? $"{order.GirlScout.FirstName} {order.GirlScout.LastName}" : null,
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

        // Update payment method, notes, and scout
        order.PaymentMethod = req.PaymentMethod ?? order.PaymentMethod;
        order.Notes = req.Notes;
        order.GirlScoutId = req.GirlScoutId;
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
        BoothSession? session = null;

        if (sessionId.HasValue)
        {
            session = await _dbContext.BoothSessions.FindAsync(sessionId.Value);
            orders = await _dbContext.Orders
                .Include(o => o.LineItems).ThenInclude(li => li.Product)
                .Include(o => o.GirlScout)
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
                .Include(o => o.GirlScout)
                .Where(o => o.OrderType == "Booth Sale" && o.OrderedAt.Date == targetDate)
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();
            fileLabel = $"booth-sales-{targetDate:yyyy-MM-dd}";
        }

        var sb = new StringBuilder();

        // Session info header
        if (session != null)
        {
            sb.AppendLine($"Booth Session: {session.Location}");
            sb.AppendLine($"Started: {session.StartedAt:yyyy-MM-dd HH:mm}");
            if (session.EndedAt.HasValue)
                sb.AppendLine($"Ended: {session.EndedAt.Value:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Scout Count: {session.ScoutCount}");
            if (!string.IsNullOrWhiteSpace(session.Notes))
                sb.AppendLine($"Notes: {session.Notes}");
            sb.AppendLine();
        }

        sb.AppendLine("Sale #,Time,Scout,Products,Payment Method,Boxes,Total,Notes");

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
            var scoutName = order.GirlScout != null
                ? $"{order.GirlScout.FirstName} {order.GirlScout.LastName}"
                : "";

            sb.AppendLine($"{counter},{order.CreatedAt:HH:mm},\"{EscapeCsv(scoutName)}\",\"{EscapeCsv(products)}\",{order.PaymentMethod},{order.TotalQty},{order.TotalPrice:F2},\"{EscapeCsv(notes)}\"");
            counter++;
        }

        sb.AppendLine();
        sb.AppendLine($"TOTALS,,,,,{orders.Sum(o => o.TotalQty ?? 0)},{orders.Sum(o => o.TotalPrice ?? 0):F2},");

        // Per-scout breakdown
        var scoutGroups = orders
            .Where(o => o.GirlScout != null)
            .GroupBy(o => new { o.GirlScoutId, Name = $"{o.GirlScout!.FirstName} {o.GirlScout.LastName}" })
            .Select(g => new
            {
                g.Key.Name,
                Sales = g.Count(),
                Boxes = g.Sum(o => o.TotalQty ?? 0),
                Revenue = g.Sum(o => o.TotalPrice ?? 0)
            })
            .OrderByDescending(g => g.Boxes)
            .ToList();

        if (scoutGroups.Any())
        {
            sb.AppendLine();
            sb.AppendLine("PER-SCOUT BREAKDOWN");
            sb.AppendLine("Scout,Sales,Boxes,Revenue");
            foreach (var sg in scoutGroups)
            {
                sb.AppendLine($"\"{EscapeCsv(sg.Name)}\",{sg.Sales},{sg.Boxes},{sg.Revenue:F2}");
            }

            var unattributed = orders.Where(o => o.GirlScout == null).ToList();
            if (unattributed.Any())
            {
                sb.AppendLine($"\"(Unattributed)\",{unattributed.Count},{unattributed.Sum(o => o.TotalQty ?? 0)},{unattributed.Sum(o => o.TotalPrice ?? 0):F2}");
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{fileLabel}.csv");
    }

    // ───────── BOOTH SESSION HISTORY ─────────
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var sessions = await _dbContext.BoothSessions
            .Include(s => s.Orders)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var sessionRows = sessions.Select(s => new BoothSessionRow
        {
            Id = s.Id,
            Location = s.Location,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            ScoutCount = s.ScoutCount,
            Notes = s.Notes,
            SaleCount = s.Orders.Count,
            TotalBoxes = s.Orders.Sum(o => o.TotalQty ?? 0),
            TotalRevenue = s.Orders.Sum(o => o.TotalPrice ?? 0)
        }).ToList();

        // Per-scout breakdown across all booth sales
        var allBoothOrders = await _dbContext.Orders
            .Include(o => o.GirlScout)
            .Where(o => o.OrderType == "Booth Sale" && o.GirlScoutId != null)
            .ToListAsync();

        var scoutBreakdown = allBoothOrders
            .GroupBy(o => new { o.GirlScoutId, Name = $"{o.GirlScout!.FirstName} {o.GirlScout.LastName}" })
            .Select(g => new ScoutContribution
            {
                ScoutId = g.Key.GirlScoutId!.Value,
                ScoutName = g.Key.Name,
                SaleCount = g.Count(),
                TotalBoxes = g.Sum(o => o.TotalQty ?? 0),
                TotalRevenue = g.Sum(o => o.TotalPrice ?? 0)
            })
            .OrderByDescending(s => s.TotalBoxes)
            .ToList();

        var completedSessions = sessionRows.Where(s => s.EndedAt != null).ToList();

        var vm = new BoothHistoryViewModel
        {
            Sessions = sessionRows,
            TotalSessions = sessions.Count,
            TotalBoxesSold = sessionRows.Sum(s => s.TotalBoxes),
            TotalRevenue = sessionRows.Sum(s => s.TotalRevenue),
            AvgBoxesPerSession = completedSessions.Any() ? (decimal)completedSessions.Average(s => s.TotalBoxes) : 0,
            AvgRevenuePerSession = completedSessions.Any() ? completedSessions.Average(s => s.TotalRevenue) : 0,
            ScoutBreakdown = scoutBreakdown
        };

        return View(vm);
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
    public List<BoothInventoryInput>? Inventory { get; set; }
}

public class BoothInventoryInput
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
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
    public Guid? GirlScoutId { get; set; }
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
    public Guid? GirlScoutId { get; set; }
    public List<BoothSaleItem>? Items { get; set; }
}

public class DeleteBoothSaleRequest
{
    public Guid OrderId { get; set; }
}
