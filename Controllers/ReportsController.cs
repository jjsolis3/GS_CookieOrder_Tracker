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
            .Where(o => pendingStatuses.Contains(o.Status) && o.OrderType != "Booth Sale")
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
            GeneratedAt = DateTime.Now
        };

        return View(model);
    }

    // ───────── PAYBACK / COLLECTION REPORT ─────────
    public async Task<IActionResult> Payback(DateTime? dateFrom, DateTime? dateTo)
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
            GeneratedAt = DateTime.Now
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
            GeneratedAt = DateTime.Now
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

        // Get inventory batches
        var batches = await _dbContext.InventoryBatches
            .Include(b => b.Product)
            .Where(b => b.ReceivedAt >= fromUtc && b.ReceivedAt < toUtc)
            .OrderByDescending(b => b.ReceivedAt)
            .ToListAsync();

        // Get current stock levels
        var currentStock = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ProductStockLevel
            {
                ProductId = p.Id,
                ProductName = p.Name,
                CurrentStock = _dbContext.InventoryBatches
                    .Where(b => b.ProductId == p.Id)
                    .Sum(b => b.QuantityBoxes - b.QuantitySold - b.QuantityReturned),
                TotalReceived = _dbContext.InventoryBatches
                    .Where(b => b.ProductId == p.Id && b.ReceivedAt >= fromUtc && b.ReceivedAt < toUtc)
                    .Sum(b => b.QuantityBoxes),
                TotalSold = _dbContext.InventoryBatches
                    .Where(b => b.ProductId == p.Id && b.ReceivedAt >= fromUtc && b.ReceivedAt < toUtc)
                    .Sum(b => b.QuantitySold),
                TotalReturned = _dbContext.InventoryBatches
                    .Where(b => b.ProductId == p.Id && b.ReceivedAt >= fromUtc && b.ReceivedAt < toUtc)
                    .Sum(b => b.QuantityReturned)
            })
            .ToListAsync();

        var model = new InventoryReportViewModel
        {
            DateFrom = fromDate,
            DateTo = toDate,
            Batches = batches,
            StockLevels = currentStock,
            TotalReceived = batches.Sum(b => b.QuantityBoxes),
            TotalSold = batches.Sum(b => b.QuantitySold),
            TotalReturned = batches.Sum(b => b.QuantityReturned),
            GeneratedAt = DateTime.Now
        };

        return View(model);
    }

    // ───────── INVENTORY RECEIPT (for a specific batch) ─────────
    public async Task<IActionResult> InventoryReceipt(Guid id)
    {
        var batch = await _dbContext.InventoryBatches
            .Include(b => b.Product)
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
            .Include(b => b.Product)
            .Where(b => batchIds.Contains(b.Id))
            .OrderBy(b => b.ReceivedAt)
            .ToListAsync();

        if (!batches.Any())
            return NotFound("No batches found.");

        return View(batches);
    }
}
