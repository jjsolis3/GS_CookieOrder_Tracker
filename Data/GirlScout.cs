using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("girl_scouts")]
public class GirlScout
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("first_name")]
    [MaxLength(100)]
    public string FirstName { get; set; } = "";

    [Column("last_name")]
    [MaxLength(100)]
    public string LastName { get; set; } = "";

    [Column("troop_number")]
    [MaxLength(50)]
    public string? TroopNumber { get; set; }

    public List<Order> Orders { get; set; } = new();
    public List<InventoryBatch> InventoryBatches { get; set; } = new();
}
