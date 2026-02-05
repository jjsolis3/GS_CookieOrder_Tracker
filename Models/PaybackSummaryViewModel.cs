namespace GS_CookieOrder_Tracker.Models;

public class PaybackSummaryViewModel
{
    public List<PaybackProductRow> ByProduct { get; set; } = new();
    public List<PaybackProductRow> PaidByProduct { get; set; } = new();
    public decimal TotalOwedFromSales { get; set; }
    public decimal TotalReturnedValue { get; set; }
    public decimal TotalOwed { get; set; }
    public decimal TotalPaid { get; set; }
    public int TotalBoxesPaid { get; set; }
    public decimal TotalRemaining => TotalOwed - TotalPaid;

    /// <summary>Recent payment records with running total.</summary>
    public List<PaybackPaymentRow> RecentPayments { get; set; } = new();
}

public class PaybackProductRow
{
    public string ProductName { get; set; } = "";
    public int BoxesSold { get; set; }
    public decimal PricePerBox { get; set; }
    public decimal AmountOwed => BoxesSold * PricePerBox;
}

public class PaybackPaymentRow
{
    public Guid Id { get; set; }
    public DateTime PaidAt { get; set; }
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Notes { get; set; }
    public string? OrderInfo { get; set; }
    public Guid? OrderId { get; set; }
    public decimal RunningTotal { get; set; }
}
