using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Helpers;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
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

        // Paid by product: line items from orders that have been paid back
        var paidOrderIds = await _dbContext.Paybacks
            .Where(p => p.OrderId != null)
            .Select(p => p.OrderId!.Value)
            .Distinct()
            .ToListAsync();

        var paidByProductFromOrders = await _dbContext.OrderLineItems
            .Include(li => li.Order)
            .Include(li => li.Product)
            .Where(li => li.OrderId != null && paidOrderIds.Contains(li.OrderId))
            .GroupBy(li => new { li.Product!.Name, li.Product.PricePerBox })
            .Select(g => new PaybackProductRow
            {
                ProductName = g.Key.Name,
                PricePerBox = g.Key.PricePerBox,
                BoxesSold = g.Sum(li => li.QuantityBoxes)
            })
            .ToListAsync();

        // Also include product-based paybacks (no matching order)
        var paidByProductDirect = await _dbContext.Paybacks
            .Include(p => p.Product)
            .Where(p => p.ProductId != null && p.Product != null && p.QuantityBoxes != null)
            .GroupBy(p => new { p.Product!.Name, p.Product.PricePerBox })
            .Select(g => new PaybackProductRow
            {
                ProductName = g.Key.Name,
                PricePerBox = g.Key.PricePerBox,
                BoxesSold = g.Sum(p => p.QuantityBoxes ?? 0)
            })
            .ToListAsync();

        // Merge both sources
        var paidByProduct = paidByProductFromOrders
            .Concat(paidByProductDirect)
            .GroupBy(r => new { r.ProductName, r.PricePerBox })
            .Select(g => new PaybackProductRow
            {
                ProductName = g.Key.ProductName,
                PricePerBox = g.Key.PricePerBox,
                BoxesSold = g.Sum(r => r.BoxesSold)
            })
            .OrderBy(r => r.ProductName)
            .ToList();

        // Subtract the value of returned product (returns remove payback responsibility)
        var returnedValue = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .SumAsync(r => (decimal?)((r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase) * r.Product.PricePerBox)) ?? 0m;

        var totalOwed = owedFromSales - returnedValue;
        if (totalOwed < 0) totalOwed = 0;

        var totalPaid = await _dbContext.Paybacks.SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var totalBoxesPaid = paidByProduct.Sum(r => r.BoxesSold);

        var recentPaymentsRaw = await _dbContext.Paybacks
            .Include(p => p.Order).ThenInclude(o => o!.Customer)
            .Include(p => p.Product)
            .OrderByDescending(p => p.PaidAt)
            .Take(20)
            .Select(p => new PaybackPaymentRow
            {
                Id = p.Id,
                PaidAt = p.PaidAt,
                Amount = p.Amount,
                Method = p.Method,
                Notes = p.Notes,
                OrderId = p.OrderId,
                OrderInfo = p.Order != null
                    ? p.Order.OrderedAt.ToPacific().ToString("yyyy-MM-dd") + " - " + (p.Order.Customer != null ? p.Order.Customer.Name : "")
                    : p.Product != null
                        ? p.Product.Name + " x" + (p.QuantityBoxes ?? 0)
                        : ""
            })
            .ToListAsync();

        // Calculate running total (from newest to oldest, so reverse for cumulative)
        var runningTotal = totalPaid;
        foreach (var payment in recentPaymentsRaw)
        {
            payment.RunningTotal = runningTotal;
            runningTotal -= payment.Amount;
        }

        var vm = new PaybackSummaryViewModel
        {
            ByProduct = owedByProduct,
            PaidByProduct = paidByProduct,
            TotalOwedFromSales = owedFromSales,
            TotalReturnedValue = returnedValue,
            TotalOwed = totalOwed,
            TotalPaid = totalPaid,
            TotalBoxesPaid = totalBoxesPaid,
            RecentPayments = recentPaymentsRaw
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Orders already paid back — exclude from dropdown to prevent double-paying
        var alreadyPaidOrderIds = await _dbContext.Paybacks
            .Where(p => p.OrderId != null)
            .Select(p => p.OrderId!.Value)
            .Distinct()
            .ToListAsync();

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Where(o => o.OrderType != "Online Delivery"
                && !alreadyPaidOrderIds.Contains(o.Id))
            .OrderByDescending(o => o.OrderedAt)
            .Select(o => new { o.Id, o.OrderedAt, CustomerName = o.Customer!.Name, o.OrderType, o.TotalPrice })
            .ToListAsync();

        var productCards = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new ProductCardItem
            {
                Id = p.Id,
                Name = p.Name,
                PricePerBox = p.PricePerBox,
                BoxesPerCase = p.BoxesPerCase,
                ImagePath = p.ImagePath
            })
            .ToListAsync();

        var viewModel = new PaybackCreateViewModel
        {
            PaidAt = DateTime.UtcNow,
            Orders = orders.Select(o => new SelectListItem(
                $"{o.OrderedAt:yyyy-MM-dd} - {o.CustomerName} ({o.OrderType}) - {o.TotalPrice:C}",
                o.Id.ToString())).ToList(),
            OrderAmounts = orders.ToDictionary(o => o.Id.ToString(), o => o.TotalPrice ?? 0m),
            PaymentMethods = new List<SelectListItem>
            {
                new("Check", "Check"),
                new("Cash", "Cash"),
                new("Bank Transfer", "Bank Transfer"),
                new("Other", "Other")
            },
            ProductCards = productCards
        };

        return View(viewModel);
    }

    // ───────── AJAX: Create multiple paybacks ─────────
    [HttpPost]
    public async Task<IActionResult> CreateMultiple([FromBody] CreateMultiplePaybacksRequest req)
    {
        var hasOrderItems = req.Items != null && req.Items.Any(i => i.OrderId != Guid.Empty && i.Amount > 0);
        var hasProductItems = req.ProductItems != null && req.ProductItems.Any(i => i.ProductId != Guid.Empty && i.Amount > 0);

        if (!hasOrderItems && !hasProductItems)
            return BadRequest(new { error = "At least one order or product is required." });

        var paidAt = DateTime.SpecifyKind(
            string.IsNullOrEmpty(req.PaidAt) ? DateTime.UtcNow : DateTime.Parse(req.PaidAt),
            DateTimeKind.Utc);

        // Double-pay prevention: check which orders are already paid back
        if (hasOrderItems)
        {
            var requestedOrderIds = req.Items!.Where(i => i.OrderId != Guid.Empty).Select(i => i.OrderId).ToList();
            var alreadyPaid = await _dbContext.Paybacks
                .Where(p => p.OrderId != null && requestedOrderIds.Contains(p.OrderId.Value))
                .Select(p => p.OrderId!.Value)
                .Distinct()
                .ToListAsync();

            if (alreadyPaid.Count > 0)
                return BadRequest(new { error = $"{alreadyPaid.Count} order(s) have already been paid back. Please refresh the page." });
        }

        // Order-based paybacks
        if (hasOrderItems)
        {
            foreach (var item in req.Items!)
            {
                if (item.OrderId == Guid.Empty || item.Amount <= 0) continue;

                _dbContext.Paybacks.Add(new Payback
                {
                    Id = Guid.NewGuid(),
                    OrderId = item.OrderId,
                    CustomerId = null,
                    PaidAt = paidAt,
                    Amount = item.Amount,
                    Method = req.Method ?? "Check",
                    Notes = req.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        // Product-based paybacks (no matching order)
        if (hasProductItems)
        {
            foreach (var item in req.ProductItems!)
            {
                if (item.ProductId == Guid.Empty || item.Amount <= 0) continue;

                _dbContext.Paybacks.Add(new Payback
                {
                    Id = Guid.NewGuid(),
                    OrderId = null,
                    ProductId = item.ProductId,
                    QuantityBoxes = item.QuantityBoxes,
                    CustomerId = null,
                    PaidAt = paidAt,
                    Amount = item.Amount,
                    Method = req.Method ?? "Check",
                    Notes = req.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Update payment ─────────
    [HttpPost]
    public async Task<IActionResult> UpdatePayment([FromBody] UpdatePaymentRequest req)
    {
        var payback = await _dbContext.Paybacks.FindAsync(req.PaymentId);
        if (payback == null) return NotFound(new { error = "Payment not found." });

        payback.Amount = req.Amount;
        payback.Method = req.Method;
        payback.Notes = req.Notes;
        if (!string.IsNullOrEmpty(req.PaidAt))
            payback.PaidAt = DateTime.SpecifyKind(DateTime.Parse(req.PaidAt), DateTimeKind.Utc);
        payback.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Delete payment ─────────
    [HttpPost]
    public async Task<IActionResult> DeletePayment([FromBody] DeletePaymentRequest req)
    {
        var payback = await _dbContext.Paybacks.FindAsync(req.PaymentId);
        if (payback == null) return NotFound(new { error = "Payment not found." });

        _dbContext.Paybacks.Remove(payback);
        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class UpdatePaymentRequest
{
    public Guid PaymentId { get; set; }
    public string? PaidAt { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Notes { get; set; }
}

public class DeletePaymentRequest
{
    public Guid PaymentId { get; set; }
}

public class CreateMultiplePaybacksRequest
{
    public string? PaidAt { get; set; }
    public string? Method { get; set; }
    public string? Notes { get; set; }
    public List<PaybackOrderItem> Items { get; set; } = new();
    public List<PaybackProductItem>? ProductItems { get; set; }
}

public class PaybackOrderItem
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}

public class PaybackProductItem
{
    public Guid ProductId { get; set; }
    public int QuantityBoxes { get; set; }
    public decimal Amount { get; set; }
}
