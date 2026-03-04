using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly AppDbContext _dbContext;

    public ReportsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ───────── REPORTS INDEX ─────────
    public IActionResult Index()
    {
        return View();
    }

    // ───────── PENDING ORDERS FULFILLMENT REPORT ─────────
    public async Task<IActionResult> PendingOrders()
    {
        var pendingStatuses = new[] { "Pending", "Confirmed" };

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => pendingStatuses.Contains(o.Status) && o.OrderType == "Direct Sale")
            .OrderBy(o => o.DeliveryDate ?? DateOnly.MaxValue)
            .ThenBy(o => o.OrderedAt)
            .ToListAsync();

        // Aggregate products needed
        var productSummary = orders
            .SelectMany(o => o.LineItems)
            .GroupBy(li => new { li.ProductId, li.Product?.Name })
            .Select(g => new ProductSummaryItem
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name ?? "Unknown",
                TotalBoxes = g.Sum(li => li.QuantityBoxes),
                TotalValue = g.Sum(li => li.QuantityBoxes * li.UnitPrice)
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        var model = new PendingOrdersReportViewModel
        {
            Orders = orders,
            ProductSummary = productSummary,
            TotalOrders = orders.Count,
            TotalBoxes = productSummary.Sum(p => p.TotalBoxes),
            TotalValue = productSummary.Sum(p => p.TotalValue),
            GeneratedAt = DateTime.UtcNow
        };

        return View(model);
    }

    // ───────── ORDER SUMMARY REPORT (custom search/filter) ─────────
    public async Task<IActionResult> OrderSummary(string? customer, string? status, string? orderType, string? dateFrom, string? dateTo)
    {
        var query = _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .AsQueryable();

        // Customer name search (case-insensitive partial match)
        if (!string.IsNullOrWhiteSpace(customer))
        {
            var search = customer.Trim().ToLower();
            query = query.Where(o => o.Customer != null && o.Customer.Name.ToLower().Contains(search));
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        // Order type filter
        if (!string.IsNullOrWhiteSpace(orderType))
            query = query.Where(o => o.OrderType == orderType);

        // Date range filter
        if (!string.IsNullOrWhiteSpace(dateFrom) && DateOnly.TryParse(dateFrom, out var dfrom))
        {
            var fromUtc = DateTime.SpecifyKind(dfrom.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(o => o.OrderedAt >= fromUtc);
        }
        if (!string.IsNullOrWhiteSpace(dateTo) && DateOnly.TryParse(dateTo, out var dto))
        {
            var toUtc = DateTime.SpecifyKind(dto.ToDateTime(TimeOnly.MinValue).AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.OrderedAt < toUtc);
        }

        var orders = await query
            .OrderBy(o => o.DeliveryDate ?? DateOnly.MaxValue)
            .ThenBy(o => o.OrderedAt)
            .ToListAsync();

        var productSummary = orders
            .SelectMany(o => o.LineItems)
            .GroupBy(li => new { li.ProductId, li.Product?.Name })
            .Select(g => new ProductSummaryItem
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name ?? "Unknown",
                TotalBoxes = g.Sum(li => li.QuantityBoxes),
                TotalValue = g.Sum(li => li.QuantityBoxes * li.UnitPrice)
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        // Build human-readable filter description
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer)) filters.Add($"Customer: \"{customer}\"");
        if (!string.IsNullOrWhiteSpace(status)) filters.Add($"Status: {status}");
        if (!string.IsNullOrWhiteSpace(orderType)) filters.Add($"Type: {orderType}");
        if (!string.IsNullOrWhiteSpace(dateFrom)) filters.Add($"From: {dateFrom}");
        if (!string.IsNullOrWhiteSpace(dateTo)) filters.Add($"To: {dateTo}");

        var model = new OrderSummaryReportViewModel
        {
            Orders = orders,
            ProductSummary = productSummary,
            TotalOrders = orders.Count,
            TotalBoxes = productSummary.Sum(p => p.TotalBoxes),
            TotalValue = productSummary.Sum(p => p.TotalValue),
            GeneratedAt = DateTime.UtcNow,
            CustomerSearch = customer,
            StatusFilter = status,
            OrderTypeFilter = orderType,
            DateFrom = dateFrom,
            DateTo = dateTo,
            FilterDescription = filters.Any() ? string.Join(" | ", filters) : "All Orders"
        };

        return View(model);
    }

    // ───────── BOOTH SESSION REPORT ─────────
    public async Task<IActionResult> BoothReport(Guid? sessionId)
    {
        // Load all sessions for the selector dropdown
        var allSessions = await _dbContext.BoothSessions
            .Include(s => s.BoothSales)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();

        var sessionOptions = allSessions.Select(s => new BoothSessionOption
        {
            Id = s.Id,
            Label = $"{s.StartedAt:yyyy-MM-dd} — {s.Location} ({s.BoothSales.Select(bs => bs.SaleGroupId).Distinct().Count()} sales, {s.BoothSales.Sum(bs => bs.QuantityBoxes)} boxes)"
        }).ToList();

        var vm = new BoothReportViewModel
        {
            AvailableSessions = sessionOptions,
            SelectedSessionId = sessionId,
            GeneratedAt = DateTime.UtcNow
        };

        // If a session is selected, load its data
        if (sessionId.HasValue)
        {
            var session = allSessions.FirstOrDefault(s => s.Id == sessionId.Value);
            if (session == null)
                return View(vm);

            var boothSales = await _dbContext.BoothSales
                .Include(bs => bs.Product)
                .Include(bs => bs.GirlScout)
                .Where(bs => bs.BoothSessionId == sessionId.Value)
                .OrderBy(bs => bs.CreatedAt)
                .ToListAsync();

            // Group booth sale rows into sale transactions
            var sales = boothSales
                .GroupBy(bs => bs.SaleGroupId ?? bs.Id)
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
                .OrderBy(s => s.CreatedAt)
                .ToList();

            // Session info
            vm.Location = session.Location;
            vm.StartedAt = session.StartedAt;
            vm.EndedAt = session.EndedAt;
            vm.ScoutCount = session.ScoutCount;
            vm.SessionNotes = session.Notes;

            if (session.EndedAt.HasValue)
            {
                var dur = session.EndedAt.Value - session.StartedAt;
                vm.Duration = dur.TotalHours >= 1
                    ? $"{(int)dur.TotalHours}h {dur.Minutes}m"
                    : $"{(int)dur.TotalMinutes}m";
            }
            else
            {
                vm.Duration = "Active";
            }

            // KPIs
            vm.TotalSales = sales.Count;
            vm.TotalBoxes = boothSales.Sum(bs => bs.QuantityBoxes);
            vm.TotalRevenue = boothSales.Sum(bs => bs.QuantityBoxes * bs.UnitPrice);
            vm.TotalDonations = session.TotalDonations;
            vm.UsePersonalInventory = session.UsePersonalInventory;
            vm.Sales = sales;

            // Product summary (from booth_sales rows)
            vm.ProductSummary = boothSales
                .GroupBy(bs => bs.Product?.Name ?? "Unknown")
                .Select(g => new BoothProductSummary
                {
                    ProductName = g.Key,
                    BoxesSold = g.Sum(bs => bs.QuantityBoxes),
                    Revenue = g.Sum(bs => bs.QuantityBoxes * bs.UnitPrice)
                })
                .OrderBy(p => p.ProductName)
                .ToList();

            // Booth inventory (starting vs sold vs remaining)
            vm.InventorySummary = await _dbContext.BoothInventories
                .Include(bi => bi.Product)
                .Where(bi => bi.BoothSessionId == sessionId.Value)
                .OrderBy(bi => bi.Product!.Name)
                .Select(bi => new BoothInventorySummary
                {
                    ProductName = bi.Product!.Name,
                    Starting = bi.StartingQuantity,
                    Sold = bi.SoldQuantity,
                    Remaining = bi.StartingQuantity - bi.SoldQuantity
                })
                .ToListAsync();

            // Per-scout breakdown
            vm.ScoutBreakdown = boothSales
                .Where(bs => bs.GirlScout != null)
                .GroupBy(bs => $"{bs.GirlScout!.FirstName} {bs.GirlScout.LastName}")
                .Select(g => new BoothScoutSummary
                {
                    ScoutName = g.Key,
                    SaleCount = g.Select(bs => bs.SaleGroupId).Distinct().Count(),
                    TotalBoxes = g.Sum(bs => bs.QuantityBoxes),
                    TotalRevenue = g.Sum(bs => bs.QuantityBoxes * bs.UnitPrice)
                })
                .OrderByDescending(s => s.TotalBoxes)
                .ToList();

            // Payment method breakdown
            vm.PaymentBreakdown = boothSales
                .GroupBy(bs => bs.PaymentMethod ?? "Unknown")
                .Select(g => new BoothPaymentSummary
                {
                    Method = g.Key,
                    Count = g.Select(bs => bs.SaleGroupId).Distinct().Count(),
                    Total = g.Sum(bs => bs.QuantityBoxes * bs.UnitPrice)
                })
                .OrderByDescending(p => p.Total)
                .ToList();
        }

        return View(vm);
    }

    // ───────── ONLINE ORDERS FULFILLMENT REPORT ─────────
    public async Task<IActionResult> OnlineOrders()
    {
        var pendingStatuses = new[] { "Pending", "Confirmed" };

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => pendingStatuses.Contains(o.Status) && o.OrderType == "Online Delivery")
            .OrderBy(o => o.DeliveryDate ?? DateOnly.MaxValue)
            .ThenBy(o => o.OrderedAt)
            .ToListAsync();

        // Aggregate products needed
        var productSummary = orders
            .SelectMany(o => o.LineItems)
            .GroupBy(li => new { li.ProductId, li.Product?.Name })
            .Select(g => new ProductSummaryItem
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name ?? "Unknown",
                TotalBoxes = g.Sum(li => li.QuantityBoxes),
                TotalValue = g.Sum(li => li.QuantityBoxes * li.UnitPrice)
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        var model = new OnlineOrdersReportViewModel
        {
            Orders = orders,
            ProductSummary = productSummary,
            TotalOrders = orders.Count,
            TotalBoxes = productSummary.Sum(p => p.TotalBoxes),
            TotalValue = productSummary.Sum(p => p.TotalValue),
            GeneratedAt = DateTime.UtcNow
        };

        return View(model);
    }

    // ───────── COLLECTIONS REPORT (outstanding customer payments) ─────────
    public async Task<IActionResult> Collections(DateTime? dateFrom, DateTime? dateTo)
    {
        var fromDate = dateFrom ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = dateTo ?? DateTime.UtcNow;

        // Ensure UTC
        fromDate = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        toDate = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => o.OrderedAt >= fromDate && o.OrderedAt < toDate && o.OrderType != "Booth Sale")
            .OrderBy(o => o.Customer!.Name)
            .ThenBy(o => o.OrderedAt)
            .ToListAsync();

        // Group by customer
        var customerSummaries = orders
            .Where(o => o.Customer != null)
            .GroupBy(o => new { o.CustomerId, o.Customer!.Name })
            .Select(g => new CustomerPaybackSummary
            {
                CustomerId = g.Key.CustomerId ?? Guid.Empty,
                CustomerName = g.Key.Name,
                Orders = g.ToList(),
                TotalOrdered = g.Sum(o => o.TotalPrice ?? 0),
                TotalPaid = g.Sum(o => o.PaidAmount ?? 0),
                TotalOwed = g.Sum(o => (o.TotalPrice ?? 0) - (o.PaidAmount ?? 0))
            })
            .Where(c => c.TotalOwed > 0)
            .OrderByDescending(c => c.TotalOwed)
            .ToList();

        var model = new PaybackReportViewModel
        {
            DateFrom = dateFrom ?? DateTime.UtcNow.AddMonths(-1),
            DateTo = dateTo ?? DateTime.UtcNow,
            CustomerSummaries = customerSummaries,
            TotalOrdered = customerSummaries.Sum(c => c.TotalOrdered),
            TotalPaid = customerSummaries.Sum(c => c.TotalPaid),
            TotalOwed = customerSummaries.Sum(c => c.TotalOwed),
            GeneratedAt = DateTime.UtcNow
        };

        return View("Collections", model);
    }

    // ───────── PAYBACK REPORT (what we owe the troop for cookies sold) ─────────
    public async Task<IActionResult> Payback(DateTime? dateFrom, DateTime? dateTo)
    {
        var fromDate = dateFrom ?? DateTime.UtcNow.AddMonths(-3);
        var toDate = dateTo ?? DateTime.UtcNow;

        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);

        // Orders that are PAID (paid_amount >= total_price), Delivered or Completed,
        // and NOT Online Delivery (those are already settled through the portal)
        var qualifyingStatuses = new[] { "Delivered", "Completed" };

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => o.OrderedAt >= fromUtc && o.OrderedAt < toUtc
                && o.OrderType != "Online Delivery"
                && o.OrderType != "Booth Sale"
                && qualifyingStatuses.Contains(o.Status)
                && o.PaidAmount != null && o.TotalPrice != null
                && o.PaidAmount >= o.TotalPrice
                && o.TotalPrice > 0)
            .OrderBy(o => o.OrderedAt)
            .ToListAsync();

        // Aggregate by product
        var productBreakdown = orders
            .SelectMany(o => o.LineItems)
            .GroupBy(li => new { li.Product!.Name, li.Product.PricePerBox })
            .Select(g => new PaybackProductBreakdown
            {
                ProductName = g.Key.Name,
                PricePerBox = g.Key.PricePerBox,
                BoxesSold = g.Sum(li => li.QuantityBoxes),
                AmountOwed = g.Sum(li => li.QuantityBoxes * g.Key.PricePerBox)
            })
            .OrderBy(p => p.ProductName)
            .ToList();

        // Aggregate by Girl Scout
        var byScout = orders
            .GroupBy(o => new
            {
                ScoutId = o.GirlScoutId,
                ScoutName = o.GirlScout != null ? $"{o.GirlScout.FirstName} {o.GirlScout.LastName}" : "Unassigned"
            })
            .Select(g => new PaybackScoutSummary
            {
                ScoutName = g.Key.ScoutName,
                OrderCount = g.Count(),
                TotalBoxes = g.Sum(o => o.TotalQty ?? 0),
                TotalAmount = g.Sum(o => o.TotalPrice ?? 0)
            })
            .OrderByDescending(s => s.TotalAmount)
            .ToList();

        // Subtract returns value
        var returnedValue = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .SumAsync(r => (decimal?)((r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase) * r.Product.PricePerBox)) ?? 0m;

        // Total already paid back to the troop
        var totalPaidBack = await _dbContext.Paybacks.SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var totalFromSales = productBreakdown.Sum(p => p.AmountOwed);
        var totalOwed = totalFromSales - returnedValue - totalPaidBack;
        if (totalOwed < 0) totalOwed = 0;

        var model = new TroopPaybackReportViewModel
        {
            DateFrom = fromDate,
            DateTo = toDate,
            ProductBreakdown = productBreakdown,
            ByScout = byScout,
            TotalFromSales = totalFromSales,
            TotalReturnedValue = returnedValue,
            TotalPaidBack = totalPaidBack,
            TotalOwedToTroop = totalOwed,
            TotalOrders = orders.Count,
            TotalBoxes = orders.Sum(o => o.TotalQty ?? 0),
            GeneratedAt = DateTime.UtcNow
        };

        return View(model);
    }

    // ───────── SALES SUMMARY REPORT ─────────
    public async Task<IActionResult> Sales(DateTime? dateFrom, DateTime? dateTo, string? groupBy)
    {
        var fromDate = dateFrom ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = dateTo ?? DateTime.UtcNow;
        groupBy = groupBy ?? "day";

        // Ensure UTC
        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);

        var orders = await _dbContext.Orders
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => o.OrderedAt >= fromUtc && o.OrderedAt < toUtc)
            .ToListAsync();

        // Product breakdown
        var productBreakdown = orders
            .SelectMany(o => o.LineItems)
            .GroupBy(li => new { li.ProductId, li.Product?.Name })
            .Select(g => new ProductSummaryItem
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name ?? "Unknown",
                TotalBoxes = g.Sum(li => li.QuantityBoxes),
                TotalValue = g.Sum(li => li.QuantityBoxes * li.UnitPrice)
            })
            .OrderByDescending(p => p.TotalBoxes)
            .ToList();

        // Time-based breakdown
        var timeBreakdown = orders
            .GroupBy(o => groupBy switch
            {
                "week" => o.OrderedAt.Date.AddDays(-(int)o.OrderedAt.DayOfWeek),
                "month" => new DateTime(o.OrderedAt.Year, o.OrderedAt.Month, 1),
                _ => o.OrderedAt.Date
            })
            .Select(g => new TimePeriodSales
            {
                Period = g.Key,
                OrderCount = g.Count(),
                TotalBoxes = g.Sum(o => o.TotalQty ?? 0),
                TotalRevenue = g.Sum(o => o.TotalPrice ?? 0)
            })
            .OrderBy(t => t.Period)
            .ToList();

        // By order type
        var byOrderType = orders
            .GroupBy(o => o.OrderType ?? "Unknown")
            .Select(g => new OrderTypeSummary
            {
                OrderType = g.Key,
                OrderCount = g.Count(),
                TotalBoxes = g.Sum(o => o.TotalQty ?? 0),
                TotalRevenue = g.Sum(o => o.TotalPrice ?? 0)
            })
            .OrderByDescending(t => t.TotalRevenue)
            .ToList();

        var model = new SalesReportViewModel
        {
            DateFrom = fromDate,
            DateTo = toDate,
            GroupBy = groupBy,
            ProductBreakdown = productBreakdown,
            TimeBreakdown = timeBreakdown,
            ByOrderType = byOrderType,
            TotalOrders = orders.Count,
            TotalBoxes = orders.Sum(o => o.TotalQty ?? 0),
            TotalRevenue = orders.Sum(o => o.TotalPrice ?? 0),
            TotalCollected = orders.Sum(o => o.PaidAmount ?? 0),
            GeneratedAt = DateTime.UtcNow
        };

        return View(model);
    }

    // ───────── INVENTORY REPORT ─────────
    public async Task<IActionResult> Inventory(DateTime? dateFrom, DateTime? dateTo)
    {
        var fromDate = dateFrom ?? DateTime.UtcNow.AddMonths(-3);
        var toDate = dateTo ?? DateTime.UtcNow;

        var fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc);

        // Get inventory batches with their receipts
        var batches = await _dbContext.InventoryBatches
            .Include(b => b.GirlScout)
            .Include(b => b.Receipts).ThenInclude(r => r.Product)
            .OrderByDescending(b => b.PickupDate)
            .ToListAsync();

        // Get inventory receipts for the period
        var receiptsInPeriod = await _dbContext.InventoryReceipts
            .Include(r => r.Product)
            .Where(r => r.ReceivedAt >= fromUtc && r.ReceivedAt < toUtc)
            .ToListAsync();

        // Get sold boxes (from order line items)
        var soldByProduct = await _dbContext.OrderLineItems
            .Where(li => li.InventorySource == "Personal")
            .GroupBy(li => li.ProductId)
            .Select(g => new { ProductId = g.Key, Boxes = g.Sum(li => li.QuantityBoxes) })
            .ToListAsync();

        // Get returned boxes
        var returnedByProduct = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .GroupBy(r => r.ProductId)
            .Select(g => new { ProductId = g.Key, Boxes = g.Sum(r => r.QuantityBoxes + r.QuantityCases * (r.Product != null ? r.Product.BoxesPerCase : 12)) })
            .ToListAsync();

        // Get all received boxes
        var receivedByProduct = await _dbContext.InventoryReceipts
            .Include(r => r.Product)
            .GroupBy(r => new { r.ProductId, r.Product!.Name, r.Product.BoxesPerCase })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Boxes = g.Sum(r => r.QuantityBoxes + r.QuantityCases * g.Key.BoxesPerCase) })
            .ToListAsync();

        // Calculate current stock levels
        var currentStock = receivedByProduct.Select(r =>
        {
            var sold = soldByProduct.FirstOrDefault(s => s.ProductId == r.ProductId)?.Boxes ?? 0;
            var returned = returnedByProduct.FirstOrDefault(s => s.ProductId == r.ProductId)?.Boxes ?? 0;
            var receivedInPeriod = receiptsInPeriod.Where(ri => ri.ProductId == r.ProductId).Sum(ri => ri.QuantityBoxes + ri.QuantityCases * (ri.Product?.BoxesPerCase ?? 12));

            return new ProductStockLevel
            {
                ProductId = r.ProductId,
                ProductName = r.Name,
                CurrentStock = r.Boxes - sold - returned,
                TotalReceived = receivedInPeriod,
                TotalSold = sold,
                TotalReturned = returned
            };
        }).OrderBy(s => s.ProductName).ToList();

        var model = new InventoryReportViewModel
        {
            DateFrom = fromDate,
            DateTo = toDate,
            Batches = batches,
            StockLevels = currentStock,
            TotalReceived = receiptsInPeriod.Sum(r => r.QuantityBoxes + r.QuantityCases * (r.Product?.BoxesPerCase ?? 12)),
            TotalSold = soldByProduct.Sum(s => s.Boxes),
            TotalReturned = returnedByProduct.Sum(r => r.Boxes),
            GeneratedAt = DateTime.UtcNow
        };

        return View(model);
    }

    // ───────── INVENTORY RECEIPT (for a specific batch) ─────────
    public async Task<IActionResult> InventoryReceipt(Guid id)
    {
        var batch = await _dbContext.InventoryBatches
            .Include(b => b.GirlScout)
            .Include(b => b.Receipts).ThenInclude(r => r.Product)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (batch == null) return NotFound();

        return View(batch);
    }

    // ───────── BATCH INVENTORY RECEIPT (for multiple batches) ─────────
    public async Task<IActionResult> BatchInventoryReceipt(string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return BadRequest("No batch IDs provided.");

        var batchIds = ids.Split(',')
            .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        if (!batchIds.Any())
            return BadRequest("No valid batch IDs provided.");

        var batches = await _dbContext.InventoryBatches
            .Include(b => b.GirlScout)
            .Include(b => b.Receipts).ThenInclude(r => r.Product)
            .Where(b => batchIds.Contains(b.Id))
            .OrderByDescending(b => b.PickupDate)
            .ToListAsync();

        if (!batches.Any())
            return NotFound("No batches found.");

        return View(batches);
    }
}
