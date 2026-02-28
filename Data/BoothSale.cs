using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

/// <summary>
/// Represents a single product line within a booth sale transaction.
/// Multiple rows with the same SaleGroupId form one complete sale.
/// Booth sales use Troop-provided inventory, not Girl Scout personal inventory.
/// </summary>
[Table("booth_sales")]
public class BoothSale
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booth_session_id")]
    public Guid? BoothSessionId { get; set; }

    [Column("sale_group_id")]
    public Guid? SaleGroupId { get; set; }

    [Column("booth_date")]
    public DateOnly BoothDate { get; set; }

    [Column("location")]
    public string Location { get; set; } = "";

    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("quantity_boxes")]
    public int QuantityBoxes { get; set; }

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("from_personal_inventory")]
    public bool FromPersonalInventory { get; set; }

    [Column("girl_scout_id")]
    public Guid? GirlScoutId { get; set; }

    [Column("payment_method")]
    [MaxLength(50)]
    public string? PaymentMethod { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BoothSession? BoothSession { get; set; }
    public Product? Product { get; set; }
    public GirlScout? GirlScout { get; set; }
}
