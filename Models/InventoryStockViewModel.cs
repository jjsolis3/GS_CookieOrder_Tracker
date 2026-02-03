namespace GS_CookieOrder_Tracker.Models;

public class InventoryStockViewModel
{
    public List<ProductStockRow> Products { get; set; } = new();
    public int TotalBoxesReceived { get; set; }
    public int TotalBoxesSold { get; set; }
    public int TotalBoxesReturned { get; set; }
    public int TotalBoxesOnHand { get; set; }
}

public class ProductStockRow
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int BoxesReceived { get; set; }
    public int BoxesSoldPersonal { get; set; }
    public int BoxesSoldTroop { get; set; }
    public int BoxesReturned { get; set; }
    public int BoxesOnHand => BoxesReceived - BoxesSoldPersonal - BoxesReturned;
}
