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

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public InventoryBatch? InventoryBatch { get; set; }
    public Product? Product { get; set; }
}
