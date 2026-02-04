namespace GS_CookieOrder_Tracker.Models;

public class OrdersIndexViewModel
{
    public List<GS_CookieOrder_Tracker.Data.Order> Orders { get; set; } = new();

    // KPI
    public int TotalOrders { get; set; }
    public int TotalBoxesSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
}
