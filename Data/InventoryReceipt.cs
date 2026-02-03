using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("inventory_receipts")]
public class InventoryReceipt
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("inventory_batch_id")]
    public Guid? InventoryBatchId { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("quantity_boxes")]
    public int QuantityBoxes { get; set; }

    [Column("quantity_cases")]
    public int QuantityCases { get; set; }

    public InventoryBatch? InventoryBatch { get; set; }
    public Product? Product { get; set; }
}
