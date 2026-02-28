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

// ═══════════ ORDER SUMMARY REPORT (custom search/filter) ═══════════
public class OrderSummaryReportViewModel
{
    public List<Order> Orders { get; set; } = new();
    public List<ProductSummaryItem> ProductSummary { get; set; } = new();
    public int TotalOrders { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime GeneratedAt { get; set; }

    // Filter state (for display)
    public string? CustomerSearch { get; set; }
    public string? StatusFilter { get; set; }
    public string? OrderTypeFilter { get; set; }
    public string? DateFrom { get; set; }
    public string? DateTo { get; set; }
    public string FilterDescription { get; set; } = "";
}

// ═══════════ BOOTH SESSION REPORT ═══════════
public class BoothReportViewModel
{
    // Session selector (for the filter bar)
    public List<BoothSessionOption> AvailableSessions { get; set; } = new();
    public Guid? SelectedSessionId { get; set; }

    // Session info
    public string? Location { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ScoutCount { get; set; }
    public string? SessionNotes { get; set; }
    public string Duration { get; set; } = "";

    // KPIs
    public int TotalSales { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }

    // Product summary (boxes sold)
    public List<BoothProductSummary> ProductSummary { get; set; } = new();

    // Booth inventory (starting vs sold vs remaining)
    public List<BoothInventorySummary> InventorySummary { get; set; } = new();

    // Per-scout breakdown
    public List<BoothScoutSummary> ScoutBreakdown { get; set; } = new();

    // Individual sales
    public List<Order> Orders { get; set; } = new();

    // Payment method breakdown
    public List<BoothPaymentSummary> PaymentBreakdown { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
    public bool HasData => SelectedSessionId.HasValue && TotalSales > 0;
}

public class BoothSessionOption
{
    public Guid Id { get; set; }
    public string Label { get; set; } = "";
}

public class BoothProductSummary
{
    public string ProductName { get; set; } = "";
    public int BoxesSold { get; set; }
    public decimal Revenue { get; set; }
}

public class BoothInventorySummary
{
    public string ProductName { get; set; } = "";
    public int Starting { get; set; }
    public int Sold { get; set; }
    public int Remaining { get; set; }
}

public class BoothScoutSummary
{
    public string ScoutName { get; set; } = "";
    public int SaleCount { get; set; }
    public int TotalBoxes { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class BoothPaymentSummary
{
    public string Method { get; set; } = "";
    public int Count { get; set; }
    public decimal Total { get; set; }
}
