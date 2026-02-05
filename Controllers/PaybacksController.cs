using GS_CookieOrder_Tracker.Data;
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

        var paidByProduct = await _dbContext.OrderLineItems
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
            .OrderBy(r => r.ProductName)
            .ToListAsync();

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
                    ? p.Order.OrderedAt.ToString("yyyy-MM-dd") + " - " + (p.Order.Customer != null ? p.Order.Customer.Name : "")
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
        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Where(o => o.OrderType != "Online Delivery")
            .OrderByDescending(o => o.OrderedAt)
            .Select(o => new { o.Id, o.OrderedAt, CustomerName = o.Customer!.Name, o.OrderType, o.TotalPrice })
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
            }
        };

        return View(viewModel);
    }

    // ───────── AJAX: Create multiple paybacks ─────────
    [HttpPost]
    public async Task<IActionResult> CreateMultiple([FromBody] CreateMultiplePaybacksRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "At least one order is required." });

        var paidAt = DateTime.SpecifyKind(
            string.IsNullOrEmpty(req.PaidAt) ? DateTime.UtcNow : DateTime.Parse(req.PaidAt),
            DateTimeKind.Utc);

        foreach (var item in req.Items)
        {
            if (item.OrderId == Guid.Empty || item.Amount <= 0) continue;

            var payback = new Payback
            {
                Id = Guid.NewGuid(),
                OrderId = item.OrderId,
                CustomerId = null, // Always payback to GS of GLA, no customer tracking needed
                PaidAt = paidAt,
                Amount = item.Amount,
                Method = req.Method ?? "Check",
                Notes = req.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Paybacks.Add(payback);
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
}

public class PaybackOrderItem
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
