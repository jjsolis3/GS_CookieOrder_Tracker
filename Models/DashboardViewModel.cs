namespace GS_CookieOrder_Tracker.Models;

public class DashboardViewModel
{
    // Inventory
    public int TotalBoxesOnHand { get; set; }
    public int TotalBoxesReceived { get; set; }
    public int TotalBoxesSold { get; set; }
    public int BoothBoxesSold { get; set; }

    // Orders
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public decimal TotalRevenue { get; set; }

    // Paybacks
    public decimal TotalOwed { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingBalance => TotalOwed - TotalPaid;

    // Top sellers
    public List<TopSellerRow> TopSellers { get; set; } = new();

    // Recent orders
    public List<RecentOrderRow> RecentOrders { get; set; } = new();
}

public class TopSellerRow
{
    public string ProductName { get; set; } = "";
    public int BoxesSold { get; set; }
    public decimal Revenue { get; set; }
}

public class RecentOrderRow
{
    public Guid OrderId { get; set; }
    public DateTime OrderedAt { get; set; }
    public string? CustomerName { get; set; }
    public string? OrderType { get; set; }
    public int TotalQty { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Status { get; set; }
}
