using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class InventoryController : Controller
{
    private readonly AppDbContext _dbContext;

    public InventoryController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var batches = await _dbContext.InventoryBatches
            .Include(batch => batch.GirlScout)
            .OrderByDescending(batch => batch.PickupDate)
            .ToListAsync();

        return View(batches);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new InventoryBatchCreateViewModel
        {
            GirlScouts = await BuildGirlScoutOptionsAsync(),
            Products = await BuildProductOptionsAsync()
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryBatchCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.GirlScouts = await BuildGirlScoutOptionsAsync();
            model.Products = await BuildProductOptionsAsync();
            return View(model);
        }

        var batch = new InventoryBatch
        {
            Id = Guid.NewGuid(),
            Status = model.Status,
            BatchType = model.BatchType,
            PickupDate = model.PickupDate,
            Notes = model.Notes,
            GirlScoutId = model.GirlScoutId,
            TotalBoxes = model.QuantityBoxes,
            TotalCases = model.QuantityCases
        };

        var receipt = new InventoryReceipt
        {
            Id = Guid.NewGuid(),
            InventoryBatchId = batch.Id,
            ProductId = model.ProductId,
            QuantityBoxes = model.QuantityBoxes,
            QuantityCases = model.QuantityCases
        };

        _dbContext.InventoryBatches.Add(batch);
        _dbContext.InventoryReceipts.Add(receipt);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
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
