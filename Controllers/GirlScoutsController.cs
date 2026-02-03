using GS_CookieOrder_Tracker.Data;
using GS_CookieOrder_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class GirlScoutsController : Controller
{
    private readonly AppDbContext _dbContext;

    public GirlScoutsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var scouts = await _dbContext.GirlScouts
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();

        return View(scouts);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new GirlScoutCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GirlScoutCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var scout = new GirlScout
        {
            Id = Guid.NewGuid(),
            FirstName = model.FirstName,
            LastName = model.LastName,
            TroopNumber = model.TroopNumber
        };

        _dbContext.GirlScouts.Add(scout);
        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var scout = await _dbContext.GirlScouts.FindAsync(id);
        if (scout == null) return NotFound();

        var vm = new GirlScoutCreateViewModel
        {
            FirstName = scout.FirstName,
            LastName = scout.LastName,
            TroopNumber = scout.TroopNumber
        };
        ViewData["ScoutId"] = id;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GirlScoutCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ScoutId"] = id;
            return View(model);
        }

        var scout = await _dbContext.GirlScouts.FindAsync(id);
        if (scout == null) return NotFound();

        scout.FirstName = model.FirstName;
        scout.LastName = model.LastName;
        scout.TroopNumber = model.TroopNumber;

        await _dbContext.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}
