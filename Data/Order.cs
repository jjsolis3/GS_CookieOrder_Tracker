namespace GS_CookieOrder_Tracker.Data;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
