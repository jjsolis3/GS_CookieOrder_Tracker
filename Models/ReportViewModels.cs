using GS_CookieOrder_Tracker.Data;

namespace GS_CookieOrder_Tracker.Models;

// ═══════════ PENDING ORDERS REPORT ═══════════
public class PendingOrdersReportViewModel
{
    public List<Order> Orders { get; set; } = new();
    public List<ProductSummaryItem> ProductSummary { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class ProductSummaryItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int TotalBoxes { get; set; }
    public decimal TotalValue { get; set; }
}

// ═══════════ ONLINE ORDERS REPORT ═══════════
public class OnlineOrdersReportViewModel
{
    public List<Order> Orders { get; set; } = new();
    public List<ProductSummaryItem> ProductSummary { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class OnlineProductSummaryItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int TotalBoxes { get; set; }
    public decimal TotalValue { get; set; }
}

// ═══════════ COLLECTIONS REPORT (outstanding customer payments) ═══════════
public class PaybackReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public List<CustomerPaybackSummary> CustomerSummaries { get; set; } = new();
    public decimal TotalOrdered { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class CustomerPaybackSummary
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
    public decimal TotalOrdered { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
}

// ═══════════ TROOP PAYBACK REPORT (what we owe the troop) ═══════════
public class TroopPaybackReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public List<PaybackProductBreakdown> ProductBreakdown { get; set; } = new();
    public List<PaybackScoutSummary> ByScout { get; set; } = new();
    public decimal TotalFromSales { get; set; }
    public decimal TotalReturnedValue { get; set; }
    public decimal TotalPaidBack { get; set; }
    public decimal TotalOwedToTroop { get; set; }
    public int TotalOrders { get; set; }
    public int TotalBoxes { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class PaybackProductBreakdown
{
    public string ProductName { get; set; } = "";
    public decimal PricePerBox { get; set; }
    public int BoxesSold { get; set; }
    public decimal AmountOwed { get; set; }
}

public class PaybackScoutSummary
{
    public string ScoutName { get; set; } = "";
    public int OrderCount { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalAmount { get; set; }
}

// ═══════════ SALES REPORT ═══════════
public class SalesReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string GroupBy { get; set; } = "day";
    public List<ProductSummaryItem> ProductBreakdown { get; set; } = new();
    public List<TimePeriodSales> TimeBreakdown { get; set; } = new();
    public List<OrderTypeSummary> ByOrderType { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class TimePeriodSales
{
    public DateTime Period { get; set; }
    public int OrderCount { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class OrderTypeSummary
{
    public string OrderType { get; set; } = "";
    public int OrderCount { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
}

// ═══════════ INVENTORY REPORT ═══════════
public class InventoryReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public List<InventoryBatch> Batches { get; set; } = new();
    public List<ProductStockLevel> StockLevels { get; set; } = new();
    public int TotalReceived { get; set; }
    public int TotalSold { get; set; }
    public int TotalReturned { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class ProductStockLevel
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int CurrentStock { get; set; }
    public int TotalReceived { get; set; }
    public int TotalSold { get; set; }
    public int TotalReturned { get; set; }
}
