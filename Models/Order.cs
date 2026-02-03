namespace YourApp.Data;

public class Order
{
    public Guid Id { get; set; }          // maps well to uuid
    public string OrderNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; }  // consider DateTimeOffset if using timestamptz
}
