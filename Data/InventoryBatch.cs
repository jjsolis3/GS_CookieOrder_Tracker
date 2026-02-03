using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("inventory_batches")]
public class InventoryBatch
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("batch_type")]
    [MaxLength(50)]
    public string? BatchType { get; set; }

    [Column("pickup_date")]
    public DateOnly? PickupDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("girl_scout_id")]
    public Guid? GirlScoutId { get; set; }

    [Column("total_boxes")]
    public int? TotalBoxes { get; set; }

    [Column("total_cases")]
    public int? TotalCases { get; set; }

    public GirlScout? GirlScout { get; set; }
    public List<InventoryReceipt> Receipts { get; set; } = new();
}
