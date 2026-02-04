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

    // Quick-add form data
    public List<SelectListItem> Products { get; set; } = new();
    public Dictionary<string, decimal> ProductPrices { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();
}
