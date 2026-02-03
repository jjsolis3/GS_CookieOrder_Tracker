using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("order_line_items")]
public class OrderLineItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("quantity_boxes")]
    public int QuantityBoxes { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// "Personal" = deducts from the girl scout's personal inventory.
    /// "Troop"    = uses troop-provided stock (no personal inventory deduction).
    /// </summary>
    [Column("inventory_source")]
    [MaxLength(20)]
    public string InventorySource { get; set; } = "Personal";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Order? Order { get; set; }
    public Product? Product { get; set; }
}
