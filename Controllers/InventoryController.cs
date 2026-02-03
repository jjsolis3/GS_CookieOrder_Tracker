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

    /// <summary>Current stock = boxes received via inventory receipts minus boxes sold from personal inventory.</summary>
    public async Task<IActionResult> Stock()
    {
        var received = await _dbContext.InventoryReceipts
            .Include(r => r.Product)
            .GroupBy(r => new { r.ProductId, r.Product!.Name })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Boxes = g.Sum(r => r.QuantityBoxes + r.QuantityCases * (r.Product!.BoxesPerCase)) })
            .ToListAsync();

        var soldPersonal = await _dbContext.OrderLineItems
            .Where(li => li.InventorySource == "Personal")
            .GroupBy(li => li.ProductId)
            .Select(g => new { ProductId = g.Key, Boxes = g.Sum(li => li.QuantityBoxes) })
            .ToListAsync();

        var soldTroop = await _dbContext.OrderLineItems
            .Where(li => li.InventorySource == "Troop")
            .GroupBy(li => li.ProductId)
            .Select(g => new { ProductId = g.Key, Boxes = g.Sum(li => li.QuantityBoxes) })
            .ToListAsync();

        var products = received.Select(r =>
        {
            var sp = soldPersonal.FirstOrDefault(s => s.ProductId == r.ProductId);
            var st = soldTroop.FirstOrDefault(s => s.ProductId == r.ProductId);
            return new ProductStockRow
            {
                ProductId = r.ProductId,
                ProductName = r.Name,
                BoxesReceived = r.Boxes,
                BoxesSoldPersonal = sp?.Boxes ?? 0,
                BoxesSoldTroop = st?.Boxes ?? 0
            };
        }).OrderBy(p => p.ProductName).ToList();

        var vm = new InventoryStockViewModel
        {
            Products = products,
            TotalBoxesReceived = products.Sum(p => p.BoxesReceived),
            TotalBoxesSold = products.Sum(p => p.BoxesSoldPersonal + p.BoxesSoldTroop),
            TotalBoxesOnHand = products.Sum(p => p.BoxesOnHand)
        };

        return View(vm);
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
