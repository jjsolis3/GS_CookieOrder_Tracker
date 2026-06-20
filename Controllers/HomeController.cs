using System.Diagnostics;
using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly AppDbContext _dbContext;

    public HomeController(ILogger<HomeController> logger, AppDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        // Inventory totals
        var received = await _dbContext.InventoryReceipts
            .Include(r => r.Product)
            .SumAsync(r => (int?)(r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase)) ?? 0;

        var soldPersonal = await _dbContext.OrderLineItems
            .Where(li => li.InventorySource == "Personal")
            .SumAsync(li => (int?)li.QuantityBoxes) ?? 0;

        var soldAll = await _dbContext.OrderLineItems
            .SumAsync(li => (int?)li.QuantityBoxes) ?? 0;

        var returnedBoxes = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .SumAsync(r => (int?)(r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase)) ?? 0;

        var boothBoxesSold = await _dbContext.BoothSales
            .SumAsync(bs => (int?)bs.QuantityBoxes) ?? 0;

        // Orders
        var totalOrders = await _dbContext.Orders.CountAsync();
        var pendingOrders = await _dbContext.Orders.CountAsync(o => o.Status == "Pending");
        var totalRevenue = await _dbContext.Orders.SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

        // Paybacks (owed from non-online orders, minus value of returned product)
        var owedFromSales = await _dbContext.OrderLineItems
            .Include(li => li.Order)
            .Where(li => li.Order!.OrderType != "Online Delivery")
            .SumAsync(li => (decimal?)(li.QuantityBoxes * li.UnitPrice)) ?? 0;

        var returnedValue = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .SumAsync(r => (decimal?)((r.QuantityBoxes + r.QuantityCases * r.Product!.BoxesPerCase) * r.Product.PricePerBox)) ?? 0;

        var totalOwed = owedFromSales - returnedValue;
        if (totalOwed < 0) totalOwed = 0;

        var totalPaid = await _dbContext.Paybacks.SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Top sellers
        var topSellers = await _dbContext.OrderLineItems
            .Include(li => li.Product)
            .GroupBy(li => li.Product!.Name)
            .Select(g => new TopSellerRow
            {
                ProductName = g.Key,
                BoxesSold = g.Sum(li => li.QuantityBoxes),
                Revenue = g.Sum(li => li.QuantityBoxes * li.UnitPrice)
            })
            .OrderByDescending(r => r.BoxesSold)
            .Take(9)
            .ToListAsync();

        // Recent orders
        var recentOrders = await _dbContext.Orders
            .Include(o => o.Customer)
            .OrderByDescending(o => o.OrderedAt)
            .Take(10)
            .Select(o => new RecentOrderRow
            {
                OrderId = o.Id,
                OrderedAt = o.OrderedAt,
                CustomerName = o.Customer != null ? o.Customer.Name : "",
                OrderType = o.OrderType,
                TotalQty = o.TotalQty ?? 0,
                TotalPrice = o.TotalPrice ?? 0,
                Status = o.Status
            })
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            TotalBoxesOnHand = received - soldPersonal - returnedBoxes,
            TotalBoxesReceived = received,
            TotalBoxesSold = soldAll,
            BoothBoxesSold = boothBoxesSold,
            TotalOrders = totalOrders,
            PendingOrders = pendingOrders,
            TotalRevenue = totalRevenue,
            TotalOwed = totalOwed,
            TotalPaid = totalPaid,
            TopSellers = topSellers,
            RecentOrders = recentOrders
        };

        return View(vm);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
