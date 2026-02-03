using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("inventory_returns")]
public class InventoryReturn
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("quantity_boxes")]
    public int QuantityBoxes { get; set; }

    [Column("quantity_cases")]
    public int QuantityCases { get; set; }

    [Column("returned_at")]
    public DateTime ReturnedAt { get; set; } = DateTime.UtcNow;

    [Column("reason")]
    [MaxLength(200)]
    public string? Reason { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    public Product? Product { get; set; }
}
