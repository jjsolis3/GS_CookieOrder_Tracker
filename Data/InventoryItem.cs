namespace GS_CookieOrder_Tracker.Data;

public class InventoryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int OnHandQty { get; set; }
}
