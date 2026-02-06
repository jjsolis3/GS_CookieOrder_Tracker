using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

/// <summary>
/// Tracks the starting inventory for a booth session.
/// This is the Troop-provided inventory that doesn't affect personal inventory.
/// </summary>
[Table("booth_inventory")]
public class BoothInventory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booth_session_id")]
    public Guid BoothSessionId { get; set; }

    [Column("product_id")]
    public Guid ProductId { get; set; }

    /// <summary>
    /// The number of boxes brought to the booth at the start.
    /// </summary>
    [Column("starting_quantity")]
    public int StartingQuantity { get; set; }

    /// <summary>
    /// Running total of boxes sold from this product during the session.
    /// </summary>
    [Column("sold_quantity")]
    public int SoldQuantity { get; set; } = 0;

    /// <summary>
    /// Boxes returned to troop inventory after the session.
    /// </summary>
    [Column("returned_quantity")]
    public int ReturnedQuantity { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public BoothSession? BoothSession { get; set; }
    public Product? Product { get; set; }

    // Computed property for remaining inventory
    [NotMapped]
    public int RemainingQuantity => StartingQuantity - SoldQuantity;
}
