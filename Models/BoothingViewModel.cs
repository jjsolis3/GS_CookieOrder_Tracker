using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class BoothingViewModel
{
    public DateTime SelectedDate { get; set; } = DateTime.UtcNow.Date;
    public List<GS_CookieOrder_Tracker.Data.Order> Orders { get; set; } = new();

    // KPI
    public int TotalSales { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }

    // Active booth session (null if none)
    public BoothSessionInfo? ActiveSession { get; set; }

    // Product cards with images for the mobile-friendly grid
    public List<ProductCard> ProductCards { get; set; } = new();

    // Quick-add form data (legacy dropdown fallback)
    public List<SelectListItem> Products { get; set; } = new();
    public Dictionary<string, decimal> ProductPrices { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();

    // Girl Scouts for per-sale attribution
    public List<SelectListItem> Scouts { get; set; } = new();

    // Booth location suggestions (recent locations used)
    public List<string> RecentLocations { get; set; } = new();
}

public class BoothSessionInfo
{
    public Guid Id { get; set; }
    public string Location { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public int ScoutCount { get; set; }
    public string? Notes { get; set; }
}

public class ProductCard
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal PricePerBox { get; set; }
    public string? ImagePath { get; set; }
}

// ───────── Booth Session History ─────────
public class BoothHistoryViewModel
{
    public List<BoothSessionRow> Sessions { get; set; } = new();

    // Aggregate KPIs across all sessions
    public int TotalSessions { get; set; }
    public int TotalBoxesSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AvgBoxesPerSession { get; set; }
    public decimal AvgRevenuePerSession { get; set; }

    // Per-scout breakdown across all sessions
    public List<ScoutContribution> ScoutBreakdown { get; set; } = new();
}

public class BoothSessionRow
{
    public Guid Id { get; set; }
    public string Location { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ScoutCount { get; set; }
    public string? Notes { get; set; }
    public int SaleCount { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class ScoutContribution
{
    public Guid ScoutId { get; set; }
    public string ScoutName { get; set; } = "";
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
    public int SaleCount { get; set; }
}
