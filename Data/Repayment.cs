namespace GS_CookieOrder_Tracker.Data;

public class Repayment
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}
