using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class ProductsController : Controller
{
    private readonly AppDbContext _dbContext;

    public ProductsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = _dbContext.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(term) ||
                (p.Sku != null && p.Sku.ToLower().Contains(term)) ||
                (p.Category != null && p.Category.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "active") query = query.Where(p => p.Active);
            else if (status == "inactive") query = query.Where(p => !p.Active);
        }

        var products = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync();

        ViewBag.SearchTerm = search;
        ViewBag.StatusFilter = status;
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
            SortOrder = model.SortOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ───────── AJAX: Get product detail ─────────
    [HttpGet]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var p = await _dbContext.Products.FindAsync(id);
        if (p == null) return NotFound();

        return Json(new
        {
            id = p.Id,
            name = p.Name,
            sku = p.Sku,
            pricePerBox = p.PricePerBox,
            description = p.Description,
            boxesPerCase = p.BoxesPerCase,
            active = p.Active,
            imagePath = p.ImagePath,
            category = p.Category,
            vendor = p.Vendor,
            cost = p.Cost,
            reward = p.Reward,
            barcode = p.Barcode,
            sortOrder = p.SortOrder
        });
    }

    // ───────── AJAX: Update product ─────────
    [HttpPost]
    public async Task<IActionResult> Update([FromBody] UpdateProductRequest req)
    {
        var p = await _dbContext.Products.FindAsync(req.Id);
        if (p == null) return NotFound();

        p.Name = req.Name ?? p.Name;
        p.Sku = req.Sku;
        p.PricePerBox = req.PricePerBox ?? p.PricePerBox;
        p.Description = req.Description;
        p.BoxesPerCase = req.BoxesPerCase ?? p.BoxesPerCase;
        p.Active = req.Active ?? p.Active;
        p.ImagePath = req.ImagePath;
        p.Category = req.Category;
        p.Vendor = req.Vendor;
        p.Cost = req.Cost;
        p.Reward = req.Reward;
        p.Barcode = req.Barcode;
        p.SortOrder = req.SortOrder ?? p.SortOrder;
        p.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }

    // ───────── AJAX: Delete product ─────────
    [HttpPost]
    public async Task<IActionResult> Delete([FromBody] DeleteProductRequest req)
    {
        var p = await _dbContext.Products.FindAsync(req.Id);
        if (p == null) return NotFound();

        var inUse = await _dbContext.OrderLineItems.AnyAsync(li => li.ProductId == req.Id);
        if (inUse)
            return BadRequest(new { error = "Cannot delete a product that has been used in orders. Deactivate it instead." });

        _dbContext.Products.Remove(p);
        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true });
    }
}

public class UpdateProductRequest
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public decimal? PricePerBox { get; set; }
    public string? Description { get; set; }
    public int? BoxesPerCase { get; set; }
    public bool? Active { get; set; }
    public string? ImagePath { get; set; }
    public string? Category { get; set; }
    public string? Vendor { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Reward { get; set; }
    public string? Barcode { get; set; }
    public int? SortOrder { get; set; }
}

public class DeleteProductRequest
{
    public Guid Id { get; set; }
}
