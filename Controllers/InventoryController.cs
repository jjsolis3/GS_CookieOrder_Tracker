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
            Lines = new List<ReceiptLineViewModel> { new() }
        };
        await PopulateDropdowns(viewModel);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryBatchCreateViewModel model)
    {
        // Remove blank lines (no product selected)
        model.Lines.RemoveAll(l => l.ProductId == Guid.Empty);

        if (model.Lines.Count == 0)
        {
            ModelState.AddModelError("", "At least one product line is required.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(model);
            return View(model);
        }

        var totalBoxes = model.Lines.Sum(l => l.QuantityBoxes);
        var totalCases = model.Lines.Sum(l => l.QuantityCases);

        var batch = new InventoryBatch
        {
            Id = Guid.NewGuid(),
            Status = model.Status,
            BatchType = model.BatchType,
            PickupDate = model.PickupDate,
            Notes = model.Notes,
            GirlScoutId = model.GirlScoutId,
            TotalBoxes = totalBoxes,
            TotalCases = totalCases
        };

        _dbContext.InventoryBatches.Add(batch);

        foreach (var line in model.Lines)
        {
            var receipt = new InventoryReceipt
            {
                Id = Guid.NewGuid(),
                InventoryBatchId = batch.Id,
                ProductId = line.ProductId,
                QuantityBoxes = line.QuantityBoxes,
                QuantityCases = line.QuantityCases,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.InventoryReceipts.Add(receipt);
        }

        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Return()
    {
        var vm = new InventoryReturnCreateViewModel
        {
            Products = await BuildProductOptionsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Return(InventoryReturnCreateViewModel model)
    {
        if (model.QuantityBoxes == 0 && model.QuantityCases == 0)
        {
            ModelState.AddModelError("", "You must return at least one box or case.");
        }

        if (!ModelState.IsValid)
        {
            model.Products = await BuildProductOptionsAsync();
            return View(model);
        }

        var ret = new InventoryReturn
        {
            Id = Guid.NewGuid(),
            ProductId = model.ProductId,
            QuantityBoxes = model.QuantityBoxes,
            QuantityCases = model.QuantityCases,
            Reason = model.Reason,
            Notes = model.Notes
        };

        _dbContext.InventoryReturns.Add(ret);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Stock));
    }

    /// <summary>Current stock = boxes received minus boxes sold (personal) minus boxes returned.</summary>
    public async Task<IActionResult> Stock()
    {
        var received = await _dbContext.InventoryReceipts
            .Include(r => r.Product)
            .GroupBy(r => new { r.ProductId, r.Product!.Name, r.Product.BoxesPerCase })
            .Select(g => new { g.Key.ProductId, g.Key.Name, Boxes = g.Sum(r => r.QuantityBoxes + r.QuantityCases * g.Key.BoxesPerCase) })
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

        var returned = await _dbContext.InventoryReturns
            .Include(r => r.Product)
            .GroupBy(r => new { r.ProductId, r.Product!.BoxesPerCase })
            .Select(g => new { ProductId = g.Key.ProductId, Boxes = g.Sum(r => r.QuantityBoxes + r.QuantityCases * g.Key.BoxesPerCase) })
            .ToListAsync();

        var products = received.Select(r =>
        {
            var sp = soldPersonal.FirstOrDefault(s => s.ProductId == r.ProductId);
            var st = soldTroop.FirstOrDefault(s => s.ProductId == r.ProductId);
            var rt = returned.FirstOrDefault(s => s.ProductId == r.ProductId);
            return new ProductStockRow
            {
                ProductId = r.ProductId,
                ProductName = r.Name,
                BoxesReceived = r.Boxes,
                BoxesSoldPersonal = sp?.Boxes ?? 0,
                BoxesSoldTroop = st?.Boxes ?? 0,
                BoxesReturned = rt?.Boxes ?? 0
            };
        }).OrderBy(p => p.ProductName).ToList();

        var vm = new InventoryStockViewModel
        {
            Products = products,
            TotalBoxesReceived = products.Sum(p => p.BoxesReceived),
            TotalBoxesSold = products.Sum(p => p.BoxesSoldPersonal + p.BoxesSoldTroop),
            TotalBoxesReturned = products.Sum(p => p.BoxesReturned),
            TotalBoxesOnHand = products.Sum(p => p.BoxesOnHand)
        };

        return View(vm);
    }

    private async Task PopulateDropdowns(InventoryBatchCreateViewModel model)
    {
        model.GirlScouts = await BuildGirlScoutOptionsAsync();
        model.Products = await BuildProductOptionsAsync();
        model.Statuses = new List<SelectListItem>
        {
            new("Received", "Received"),
            new("Pending Pickup", "Pending Pickup"),
            new("In Transit", "In Transit"),
            new("Returned", "Returned")
        };
        model.BatchTypes = new List<SelectListItem>
        {
            new("Initial Order", "Initial Order"),
            new("Restock", "Restock"),
            new("Transfer", "Transfer"),
            new("Donation", "Donation")
        };
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
