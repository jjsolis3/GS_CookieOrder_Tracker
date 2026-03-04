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

    public BoothingController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ───────── HELPER: Build BoothSaleDisplay list from BoothSale rows ─────────
    private static List<BoothSaleDisplay> GroupBoothSales(List<BoothSale> boothSales)
    {
        return boothSales
            .GroupBy(bs => bs.SaleGroupId ?? bs.Id) // fallback to Id for legacy rows without SaleGroupId
            .Select(g => new BoothSaleDisplay
            {
                SaleGroupId = g.Key,
                CreatedAt = g.Min(bs => bs.CreatedAt),
                PaymentMethod = g.First().PaymentMethod,
                ScoutName = g.First().GirlScout != null
                    ? $"{g.First().GirlScout!.FirstName} {g.First().GirlScout!.LastName}"
                    : null,
                GirlScoutId = g.First().GirlScoutId,
                Notes = g.First().Notes,
                TotalQty = g.Sum(bs => bs.QuantityBoxes),
                TotalPrice = g.Sum(bs => bs.QuantityBoxes * bs.UnitPrice),
                LineItems = g.Select(bs => new BoothSaleLineItem
                {
                    ProductId = bs.ProductId,
                    ProductName = bs.Product?.Name ?? "Unknown",
                    QuantityBoxes = bs.QuantityBoxes,
                    UnitPrice = bs.UnitPrice
                }).ToList()
            })
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    // ───────── MAIN PAGE ─────────
    public async Task<IActionResult> Index(string? date)
    {
        // Parse date and ensure it's UTC
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

        // Load booth sales from booth_sales table
        List<BoothSale> boothSales;
        if (activeSession != null)
        {
            boothSales = await _dbContext.BoothSales
                .Include(bs => bs.Product)
                .Include(bs => bs.GirlScout)
                .Where(bs => bs.BoothSessionId == activeSession.Id)
                .OrderByDescending(bs => bs.CreatedAt)
                .ToListAsync();
        }
        else
        {
            // Show sales for the target date when no active session
            var targetDateOnly = DateOnly.FromDateTime(targetDate);
            boothSales = await _dbContext.BoothSales
                .Include(bs => bs.Product)
                .Include(bs => bs.GirlScout)
                .Where(bs => bs.BoothDate == targetDateOnly)
                .OrderByDescending(bs => bs.CreatedAt)
                .ToListAsync();
        }

        var sales = GroupBoothSales(boothSales);

        // Products with images for card grid
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

        var vm = new BoothingViewModel
        {
            SelectedDate = targetDate,
            Sales = sales,
            TotalSales = boothSales.Sum(bs => bs.QuantityBoxes),
            TotalRevenue = boothSales.Sum(bs => bs.QuantityBoxes * bs.UnitPrice),
            TotalCollected = boothSales.Sum(bs => bs.QuantityBoxes * bs.UnitPrice),
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
            TotalDonations = activeSession?.TotalDonations ?? 0,
            ActiveSession = activeSession != null ? new BoothSessionInfo
            {
                Id = activeSession.Id,
                Location = activeSession.Location,
                StartedAt = activeSession.StartedAt,
                ScoutCount = activeSession.ScoutCount,
                Notes = activeSession.Notes,
                UsePersonalInventory = activeSession.UsePersonalInventory,
                TotalDonations = activeSession.TotalDonations
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
            UsePersonalInventory = req.UsePersonalInventory,
            TotalDonations = 0,
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
            .Select(i => (object)new { productId = i.ProductId, starting = i.Quantity, sold = 0, remaining = i.Quantity })
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

    // ───────── ADD DONATION ─────────
    [HttpPost]
    public async Task<IActionResult> AddDonation([FromBody] AddDonationRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { error = "Donation amount must be greater than zero." });

        var session = await _dbContext.BoothSessions.FindAsync(req.SessionId);
        if (session == null) return NotFound(new { error = "Session not found." });

        session.TotalDonations += req.Amount;
        session.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            totalDonations = session.TotalDonations
        });
    }

    // ───────── QUICK ADD BOOTH SALE (writes to booth_sales table) ─────────
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

        // Resolve location from session or request
        var location = "";
        if (req.SessionId.HasValue)
        {
            var session = await _dbContext.BoothSessions.FindAsync(req.SessionId.Value);
            if (session != null) location = session.Location;
        }
        else if (!string.IsNullOrWhiteSpace(req.Location))
        {
            location = req.Location.Trim();
        }

        // Resolve scout name if provided
        string? scoutName = null;
        if (req.GirlScoutId.HasValue)
        {
            var scout = await _dbContext.GirlScouts.FindAsync(req.GirlScoutId.Value);
            if (scout != null) scoutName = $"{scout.FirstName} {scout.LastName}";
        }

        var saleGroupId = Guid.NewGuid();
        var boothDate = DateOnly.FromDateTime(targetDate);
        var notes = req.Notes?.Trim();

        // Check if this session uses personal inventory
        bool usePersonalInventory = false;
        if (req.SessionId.HasValue)
        {
            var sessionForFlag = await _dbContext.BoothSessions.FindAsync(req.SessionId.Value);
            if (sessionForFlag != null) usePersonalInventory = sessionForFlag.UsePersonalInventory;
        }

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

        // If using personal inventory, create a corresponding Order to deduct from Girl Scout inventory
        Order? personalInventoryOrder = null;
        if (usePersonalInventory)
        {
            personalInventoryOrder = new Order
            {
                Id = Guid.NewGuid(),
                OrderType = "Booth Sale",
                Status = "Completed",
                PaymentMethod = req.PaymentMethod ?? "Cash",
                IsOnlinePaid = false,
                OrderedAt = targetDate,
                Notes = $"Booth sale (personal inventory) - {location}",
                TotalQty = totalQty,
                TotalPrice = totalPrice,
                PaidAmount = totalPrice,
                BoothSessionId = req.SessionId,
                GirlScoutId = req.GirlScoutId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Orders.Add(personalInventoryOrder);
        }

        foreach (var item in req.Items)
        {
            if (!products.ContainsKey(item.ProductId)) continue;

            _dbContext.BoothSales.Add(new BoothSale
            {
                Id = Guid.NewGuid(),
                BoothSessionId = req.SessionId,
                SaleGroupId = saleGroupId,
                BoothDate = boothDate,
                Location = location,
                ProductId = item.ProductId,
                QuantityBoxes = item.Qty,
                UnitPrice = products[item.ProductId].PricePerBox,
                FromPersonalInventory = usePersonalInventory,
                GirlScoutId = req.GirlScoutId,
                PaymentMethod = req.PaymentMethod ?? "Cash",
                Notes = string.IsNullOrEmpty(notes) ? null : notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            // If using personal inventory, also create OrderLineItems to deduct from personal stock
            if (usePersonalInventory && personalInventoryOrder != null)
            {
                _dbContext.OrderLineItems.Add(new OrderLineItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = personalInventoryOrder.Id,
                    ProductId = item.ProductId,
                    QuantityBoxes = item.Qty,
                    UnitPrice = products[item.ProductId].PricePerBox,
                    InventorySource = "Personal",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Update booth inventory sold quantity (only if NOT personal inventory - troop stock tracking)
            if (!usePersonalInventory && boothInventory != null && boothInventory.TryGetValue(item.ProductId, out var bi))
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
            var sessionSales = await _dbContext.BoothSales
                .Where(bs => bs.BoothSessionId == req.SessionId.Value)
                .ToListAsync();
            sessionSaleCount = sessionSales.Select(bs => bs.SaleGroupId).Distinct().Count();
            sessionTotalBoxes = sessionSales.Sum(bs => bs.QuantityBoxes);
            sessionTotalRevenue = sessionSales.Sum(bs => bs.QuantityBoxes * bs.UnitPrice);
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
            saleGroupId,
            // Sale details for adding row to table
            sale = new
            {
                id = saleGroupId,
                time = DateTime.UtcNow.ToString("h:mm tt"),
                products = string.Join(", ", lineItemDetails.Select(li =>
                {
                    var d = (dynamic)li;
                    return $"{d.productName} x{d.qty}";
                })),
                paymentMethod = req.PaymentMethod ?? "Cash",
                totalQty = totalQty,
                totalPrice = totalPrice,
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
        var saleRows = await _dbContext.BoothSales
            .Include(bs => bs.Product)
            .Include(bs => bs.GirlScout)
            .Where(bs => bs.SaleGroupId == id || bs.Id == id)
            .ToListAsync();

        if (!saleRows.Any()) return NotFound();

        var first = saleRows.First();
        return Json(new
        {
            id = first.SaleGroupId ?? first.Id,
            paymentMethod = first.PaymentMethod,
            notes = first.Notes,
            girlScoutId = first.GirlScoutId,
            scoutName = first.GirlScout != null ? $"{first.GirlScout.FirstName} {first.GirlScout.LastName}" : null,
            lineItems = saleRows.Select(bs => new
            {
                id = bs.Id,
                productId = bs.ProductId,
                productName = bs.Product?.Name,
                quantityBoxes = bs.QuantityBoxes,
                unitPrice = bs.UnitPrice,
                lineTotal = bs.QuantityBoxes * bs.UnitPrice
            })
        });
    }

    // ───────── UPDATE SALE ─────────
    [HttpPost]
    public async Task<IActionResult> UpdateSale([FromBody] UpdateBoothSaleRequest req)
    {
        var saleRows = await _dbContext.BoothSales
            .Where(bs => bs.SaleGroupId == req.SaleGroupId || bs.Id == req.SaleGroupId)
            .ToListAsync();

        if (!saleRows.Any()) return NotFound(new { error = "Sale not found." });

        var sessionId = saleRows.First().BoothSessionId;
        var boothDate = saleRows.First().BoothDate;
        var location = saleRows.First().Location;

        if (req.Items != null && req.Items.Count > 0)
        {
            // Reverse booth inventory for old items
            if (sessionId.HasValue)
            {
                var boothInventory = await _dbContext.BoothInventories
                    .Where(bi => bi.BoothSessionId == sessionId.Value)
                    .ToDictionaryAsync(bi => bi.ProductId);

                foreach (var oldRow in saleRows)
                {
                    if (boothInventory.TryGetValue(oldRow.ProductId, out var bi))
                    {
                        bi.SoldQuantity -= oldRow.QuantityBoxes;
                        bi.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            // Remove old sale rows
            _dbContext.BoothSales.RemoveRange(saleRows);

            var productIds = req.Items.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // Re-add booth inventory for new items
            Dictionary<Guid, BoothInventory>? newBoothInventory = null;
            if (sessionId.HasValue)
            {
                newBoothInventory = await _dbContext.BoothInventories
                    .Where(bi => bi.BoothSessionId == sessionId.Value)
                    .ToDictionaryAsync(bi => bi.ProductId);
            }

            foreach (var item in req.Items)
            {
                if (!products.ContainsKey(item.ProductId) || item.Qty <= 0) continue;

                _dbContext.BoothSales.Add(new BoothSale
                {
                    Id = Guid.NewGuid(),
                    BoothSessionId = sessionId,
                    SaleGroupId = req.SaleGroupId,
                    BoothDate = boothDate,
                    Location = location,
                    ProductId = item.ProductId,
                    QuantityBoxes = item.Qty,
                    UnitPrice = products[item.ProductId].PricePerBox,
                    FromPersonalInventory = false,
                    GirlScoutId = req.GirlScoutId,
                    PaymentMethod = req.PaymentMethod ?? saleRows.First().PaymentMethod ?? "Cash",
                    Notes = req.Notes,
                    CreatedAt = saleRows.First().CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                });

                if (newBoothInventory != null && newBoothInventory.TryGetValue(item.ProductId, out var bi))
                {
                    bi.SoldQuantity += item.Qty;
                    bi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
        else
        {
            // Only updating payment method, notes, and scout (no product changes)
            foreach (var row in saleRows)
            {
                row.PaymentMethod = req.PaymentMethod ?? row.PaymentMethod;
                row.Notes = req.Notes;
                row.GirlScoutId = req.GirlScoutId;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── DELETE SALE ─────────
    [HttpPost]
    public async Task<IActionResult> DeleteSale([FromBody] DeleteBoothSaleRequest req)
    {
        var saleRows = await _dbContext.BoothSales
            .Where(bs => bs.SaleGroupId == req.SaleGroupId || bs.Id == req.SaleGroupId)
            .ToListAsync();

        if (!saleRows.Any()) return NotFound(new { error = "Sale not found." });

        // Reverse booth inventory sold quantities
        var sessionId = saleRows.First().BoothSessionId;
        if (sessionId.HasValue)
        {
            var boothInventory = await _dbContext.BoothInventories
                .Where(bi => bi.BoothSessionId == sessionId.Value)
                .ToDictionaryAsync(bi => bi.ProductId);

            foreach (var row in saleRows)
            {
                if (boothInventory.TryGetValue(row.ProductId, out var bi))
                {
                    bi.SoldQuantity -= row.QuantityBoxes;
                    if (bi.SoldQuantity < 0) bi.SoldQuantity = 0;
                    bi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        _dbContext.BoothSales.RemoveRange(saleRows);
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

        List<BoothSale> boothSales;
        string fileLabel;
        BoothSession? session = null;

        if (sessionId.HasValue)
        {
            session = await _dbContext.BoothSessions.FindAsync(sessionId.Value);
            boothSales = await _dbContext.BoothSales
                .Include(bs => bs.Product)
                .Include(bs => bs.GirlScout)
                .Where(bs => bs.BoothSessionId == sessionId.Value)
                .OrderBy(bs => bs.CreatedAt)
                .ToListAsync();
            fileLabel = session != null
                ? $"booth-{session.Location.Replace(" ", "-")}-{session.StartedAt:yyyy-MM-dd}"
                : $"booth-session-{targetDate:yyyy-MM-dd}";
        }
        else
        {
            var targetDateOnly = DateOnly.FromDateTime(targetDate);
            boothSales = await _dbContext.BoothSales
                .Include(bs => bs.Product)
                .Include(bs => bs.GirlScout)
                .Where(bs => bs.BoothDate == targetDateOnly)
                .OrderBy(bs => bs.CreatedAt)
                .ToListAsync();
            fileLabel = $"booth-sales-{targetDate:yyyy-MM-dd}";
        }

        var sales = GroupBoothSales(boothSales).OrderBy(s => s.CreatedAt).ToList();

        var sb = new StringBuilder();

        // Session info header
        if (session != null)
        {
            sb.AppendLine($"Booth Session: {session.Location}");
            sb.AppendLine($"Started: {session.StartedAt:yyyy-MM-dd HH:mm}");
            if (session.EndedAt.HasValue)
                sb.AppendLine($"Ended: {session.EndedAt.Value:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Scout Count: {session.ScoutCount}");
            sb.AppendLine($"Inventory Source: {(session.UsePersonalInventory ? "Personal" : "Troop")}");
            if (session.TotalDonations > 0)
                sb.AppendLine($"Donations: {session.TotalDonations:C}");
            if (!string.IsNullOrWhiteSpace(session.Notes))
                sb.AppendLine($"Notes: {session.Notes}");
            sb.AppendLine();
        }

        sb.AppendLine("Sale #,Time,Scout,Products,Payment Method,Boxes,Total,Notes");

        var counter = 1;
        foreach (var sale in sales)
        {
            var products = string.Join("; ", sale.LineItems.Select(li => $"{li.ProductName} x{li.QuantityBoxes}"));

            sb.AppendLine($"{counter},{sale.CreatedAt:HH:mm},\"{EscapeCsv(sale.ScoutName)}\",\"{EscapeCsv(products)}\",{sale.PaymentMethod},{sale.TotalQty},{sale.TotalPrice:F2},\"{EscapeCsv(sale.Notes)}\"");
            counter++;
        }

        sb.AppendLine();
        sb.AppendLine($"TOTALS,,,,,{sales.Sum(s => s.TotalQty)},{sales.Sum(s => s.TotalPrice):F2},");

        // Per-scout breakdown
        var scoutGroups = sales
            .Where(s => !string.IsNullOrEmpty(s.ScoutName))
            .GroupBy(s => new { s.GirlScoutId, s.ScoutName })
            .Select(g => new
            {
                Name = g.Key.ScoutName ?? "",
                Sales = g.Count(),
                Boxes = g.Sum(s => s.TotalQty),
                Revenue = g.Sum(s => s.TotalPrice)
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

            var unattributed = sales.Where(s => string.IsNullOrEmpty(s.ScoutName)).ToList();
            if (unattributed.Any())
            {
                sb.AppendLine($"\"(Unattributed)\",{unattributed.Count},{unattributed.Sum(s => s.TotalQty)},{unattributed.Sum(s => s.TotalPrice):F2}");
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
            .Include(s => s.BoothSales)
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
            UsePersonalInventory = s.UsePersonalInventory,
            SaleCount = s.BoothSales.Select(bs => bs.SaleGroupId).Distinct().Count(),
            TotalBoxes = s.BoothSales.Sum(bs => bs.QuantityBoxes),
            TotalRevenue = s.BoothSales.Sum(bs => bs.QuantityBoxes * bs.UnitPrice),
            TotalDonations = s.TotalDonations
        }).ToList();

        // Per-scout breakdown across all booth sales
        var allBoothSales = await _dbContext.BoothSales
            .Include(bs => bs.GirlScout)
            .Where(bs => bs.GirlScoutId != null)
            .ToListAsync();

        var scoutBreakdown = allBoothSales
            .GroupBy(bs => new { bs.GirlScoutId, Name = $"{bs.GirlScout!.FirstName} {bs.GirlScout.LastName}" })
            .Select(g => new ScoutContribution
            {
                ScoutId = g.Key.GirlScoutId!.Value,
                ScoutName = g.Key.Name,
                SaleCount = g.Select(bs => bs.SaleGroupId).Distinct().Count(),
                TotalBoxes = g.Sum(bs => bs.QuantityBoxes),
                TotalRevenue = g.Sum(bs => bs.QuantityBoxes * bs.UnitPrice)
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
    public bool UsePersonalInventory { get; set; }
    public List<BoothInventoryInput>? Inventory { get; set; }
}

public class AddDonationRequest
{
    public Guid SessionId { get; set; }
    public decimal Amount { get; set; }
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
    public Guid SaleGroupId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public Guid? GirlScoutId { get; set; }
    public List<BoothSaleItem>? Items { get; set; }
}

public class DeleteBoothSaleRequest
{
    public Guid SaleGroupId { get; set; }
}
