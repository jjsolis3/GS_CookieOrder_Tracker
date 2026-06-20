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
public class OrdersController : Controller
{
    private readonly AppDbContext _dbContext;

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ───────── LIST ─────────
    public async Task<IActionResult> Index(
        string? search,
        string? orderType,
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? sortBy,
        string? sortDir,
        int page = 1,
        int pageSize = 25)
    {
        var query = _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o =>
                (o.Customer != null && o.Customer.Name.ToLower().Contains(term)) ||
                (o.GirlScout != null && (o.GirlScout.FirstName.ToLower().Contains(term) || o.GirlScout.LastName.ToLower().Contains(term))) ||
                (o.Notes != null && o.Notes.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(orderType))
            query = query.Where(o => o.OrderType == orderType);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        if (dateFrom.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(dateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(o => o.OrderedAt >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.OrderedAt < toUtc);
        }

        // Get totals for KPI (filtered, excluding cancelled)
        var filteredForKpi = query.Where(o => o.Status != "Cancelled");
        var totalFiltered = await query.CountAsync();
        var kpiOrders = await filteredForKpi.CountAsync();
        var kpiBoxes = await filteredForKpi.SumAsync(o => o.TotalQty ?? 0);
        var kpiRevenue = await filteredForKpi.SumAsync(o => o.TotalPrice ?? 0);
        var kpiCollected = await filteredForKpi.SumAsync(o => o.PaidAmount ?? 0);

        // Pagination
        var totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        // Apply sorting
        var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedQueryable<Order> sorted = sortBy?.ToLower() switch
        {
            "date" => isDesc ? query.OrderByDescending(o => o.OrderedAt) : query.OrderBy(o => o.OrderedAt),
            "type" => isDesc ? query.OrderByDescending(o => o.OrderType) : query.OrderBy(o => o.OrderType),
            "customer" => isDesc ? query.OrderByDescending(o => o.Customer != null ? o.Customer.Name : "") : query.OrderBy(o => o.Customer != null ? o.Customer.Name : ""),
            "scout" => isDesc ? query.OrderByDescending(o => o.GirlScout != null ? o.GirlScout.FirstName : "") : query.OrderBy(o => o.GirlScout != null ? o.GirlScout.FirstName : ""),
            "status" => isDesc ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            "qty" => isDesc ? query.OrderByDescending(o => o.TotalQty) : query.OrderBy(o => o.TotalQty),
            "total" => isDesc ? query.OrderByDescending(o => o.TotalPrice) : query.OrderBy(o => o.TotalPrice),
            _ => query.OrderByDescending(o => o.OrderedAt) // default: newest first
        };

        var orders = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new OrdersIndexViewModel
        {
            Orders = orders,
            TotalOrders = kpiOrders,
            TotalBoxesSold = kpiBoxes,
            TotalRevenue = kpiRevenue,
            TotalCollected = kpiCollected,
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalFilteredCount = totalFiltered,
            SortBy = sortBy,
            SortDir = sortDir,
            SearchTerm = search,
            OrderTypeFilter = orderType,
            StatusFilter = status,
            DateFrom = dateFrom,
            DateTo = dateTo,
            OrderTypes = new List<SelectListItem>
            {
                new("All Types", ""),
                new("Direct Sale", "Direct Sale"),
                new("Online Delivery", "Online Delivery"),
                new("Booth Sale", "Booth Sale")
            },
            Statuses = new List<SelectListItem>
            {
                new("All Statuses", ""),
                new("Pending", "Pending"),
                new("Confirmed", "Confirmed"),
                new("Delivered", "Delivered"),
                new("Completed", "Completed"),
                new("Cancelled", "Cancelled")
            }
        };

        return View(vm);
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
            Status = isOnlinePaid ? "Completed" : "Pending",
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

        // Payment is now tracked separately via PaidAmount field, not via status

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Toggle payment status ─────────
    [HttpPost]
    public async Task<IActionResult> TogglePayment([FromBody] TogglePaymentRequest req)
    {
        var order = await _dbContext.Orders.FindAsync(req.OrderId);
        if (order == null) return NotFound();

        if (req.MarkAsPaid)
            order.PaidAmount = order.TotalPrice;
        else
            order.PaidAmount = 0m;

        order.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true, paidAmount = order.PaidAmount });
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
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
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

    // ───────── AJAX: Get data for quick-order modal ─────────
    [HttpGet]
    public async Task<IActionResult> GetQuickOrderData()
    {
        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.PricePerBox, p.ImagePath })
            .ToListAsync();

        var customers = await _dbContext.Customers
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        var scouts = await _dbContext.GirlScouts
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new { s.Id, Name = s.FirstName + " " + s.LastName })
            .ToListAsync();

        return Json(new { products, customers, scouts });
    }

    // ───────── AJAX: Quick create order (from modal) ─────────
    [HttpPost]
    public async Task<IActionResult> QuickCreate([FromBody] QuickCreateOrderRequest req)
    {
        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { error = "At least one product is required." });

        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var totalQty = req.Items.Sum(i => i.Qty);
        var totalPrice = req.Items.Sum(i => i.Qty * (products.ContainsKey(i.ProductId) ? products[i.ProductId].PricePerBox : 0));

        var isOnlinePaid = req.OrderType == "Online Delivery";
        var orderedUtc = DateTime.SpecifyKind(
            req.OrderDate.HasValue ? req.OrderDate.Value.Date : DateTime.UtcNow.Date,
            DateTimeKind.Utc);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = req.CustomerId,
            GirlScoutId = req.GirlScoutId,
            OrderType = req.OrderType ?? "Direct Sale",
            PaymentMethod = req.PaymentMethod ?? "Cash",
            IsOnlinePaid = isOnlinePaid,
            Status = isOnlinePaid ? "Completed" : "Pending",
            OrderedAt = orderedUtc,
            Notes = req.Notes,
            TotalQty = totalQty,
            TotalPrice = totalPrice,
            PaidAmount = isOnlinePaid ? totalPrice : 0m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Orders.Add(order);

        foreach (var item in req.Items)
        {
            if (!products.ContainsKey(item.ProductId) || item.Qty <= 0) continue;
            var source = req.OrderType == "Booth Sale" ? "Troop" : (req.InventorySource ?? "Personal");

            _dbContext.OrderLineItems.Add(new OrderLineItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                QuantityBoxes = item.Qty,
                UnitPrice = products[item.ProductId].PricePerBox,
                InventorySource = source,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true, orderId = order.Id, totalPrice, totalQty });
    }

    // ───────── Dropdown helpers ─────────
    private async Task PopulateDropdowns(OrderCreateViewModel model)
    {
        model.Customers = await BuildCustomerOptionsAsync();
        model.GirlScouts = await BuildGirlScoutOptionsAsync();

        var products = await _dbContext.Products
            .Where(p => p.Active)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.PricePerBox, p.ImagePath, p.BoxesPerCase })
            .ToListAsync();

        model.Products = products
            .Select(p => new SelectListItem($"{p.Name} (${p.PricePerBox})", p.Id.ToString()))
            .ToList();

        model.ProductPrices = products.ToDictionary(p => p.Id.ToString(), p => p.PricePerBox);

        model.ProductCards = products.Select(p => new ProductCardItem
        {
            Id = p.Id,
            Name = p.Name,
            PricePerBox = p.PricePerBox,
            BoxesPerCase = p.BoxesPerCase,
            ImagePath = p.ImagePath
        }).ToList();
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

    // ───────── RECEIPT: Print-friendly order view ─────────
    [HttpGet]
    public async Task<IActionResult> Receipt(Guid id)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        return View(order);
    }

    // ───────── BATCH RECEIPT: Print multiple orders at once ─────────
    [HttpGet]
    public async Task<IActionResult> BatchReceipt(string ids)
    {
        if (string.IsNullOrEmpty(ids))
            return BadRequest("No order IDs provided.");

        var orderIds = ids.Split(',')
            .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        if (!orderIds.Any())
            return BadRequest("No valid order IDs provided.");

        var orders = await _dbContext.Orders
            .Include(o => o.Customer)
            .Include(o => o.GirlScout)
            .Include(o => o.LineItems).ThenInclude(li => li.Product)
            .Where(o => orderIds.Contains(o.Id))
            .OrderBy(o => o.OrderedAt)
            .ToListAsync();

        if (!orders.Any())
            return NotFound("No orders found.");

        return View(orders);
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

        // Auto-select the first scout as default
        if (scouts.Any())
            scouts[0].Selected = true;

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

public class TogglePaymentRequest
{
    public Guid OrderId { get; set; }
    public bool MarkAsPaid { get; set; }
}

public class QuickCreateOrderRequest
{
    public string? OrderType { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? GirlScoutId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? InventorySource { get; set; }
    public DateTime? OrderDate { get; set; }
    public string? Notes { get; set; }
    public List<QuickOrderItem> Items { get; set; } = new();
}

public class QuickOrderItem
{
    public Guid ProductId { get; set; }
    public int Qty { get; set; }
}
