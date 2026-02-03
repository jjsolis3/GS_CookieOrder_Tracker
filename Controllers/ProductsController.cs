using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly AppDbContext _dbContext;

    public ProductsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _dbContext.Products
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View(products);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var viewModel = new ProductCreateViewModel();
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = model.Name,
            Sku = model.Sku,
            PricePerBox = model.PricePerBox,
            Description = model.Description,
            BoxesPerCase = model.BoxesPerCase,
            Active = model.Active,
            ImagePath = model.ImagePath,
            Category = model.Category,
            Vendor = model.Vendor,
            Cost = model.Cost,
            Reward = model.Reward,
            Barcode = model.Barcode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
