using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly AppDbContext _dbContext;

    public OrdersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _dbContext.Orders
            .Include(order => order.Customer)
            .Include(order => order.GirlScout)
            .OrderByDescending(order => order.OrderedAt)
            .ToListAsync();

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new OrderCreateViewModel
        {
            Customers = await BuildCustomerOptionsAsync(),
            GirlScouts = await BuildGirlScoutOptionsAsync(),
            Products = await BuildProductOptionsAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OrderCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Customers = await BuildCustomerOptionsAsync();
            model.GirlScouts = await BuildGirlScoutOptionsAsync();
            model.Products = await BuildProductOptionsAsync();
            return View(model);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = model.CustomerId,
            GirlScoutId = model.GirlScoutId,
            OrderType = model.OrderType,
            Status = model.Status,
            OrderedAt = model.OrderedAt,
            DeliveryDate = model.DeliveryDate,
            Notes = model.Notes,
            TotalQty = model.QuantityBoxes,
            TotalPrice = model.QuantityBoxes * model.UnitPrice,
            PaidAmount = 0m
        };

        var lineItem = new OrderLineItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = model.ProductId,
            QuantityBoxes = model.QuantityBoxes,
            UnitPrice = model.UnitPrice
        };

        _dbContext.Orders.Add(order);
        _dbContext.OrderLineItems.Add(lineItem);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<SelectListItem>> BuildCustomerOptionsAsync()
    {
        return await _dbContext.Customers
            .OrderBy(customer => customer.Name)
            .Select(customer => new SelectListItem(customer.Name, customer.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> BuildGirlScoutOptionsAsync()
    {
        var scouts = await _dbContext.GirlScouts
            .OrderBy(scout => scout.LastName)
            .ThenBy(scout => scout.FirstName)
            .Select(scout => new SelectListItem($"{scout.FirstName} {scout.LastName}", scout.Id.ToString()))
            .ToListAsync();

        scouts.Insert(0, new SelectListItem("Unassigned", ""));
        return scouts;
    }

    private async Task<List<SelectListItem>> BuildProductOptionsAsync()
    {
        return await _dbContext.Products
            .Where(product => product.Active)
            .OrderBy(product => product.Name)
            .Select(product => new SelectListItem(product.Name, product.Id.ToString()))
            .ToListAsync();
    }
}
