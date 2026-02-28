using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("booth_sessions")]
public class BoothSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("location")]
    [MaxLength(300)]
    public string Location { get; set; } = "";

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    [Column("scout_count")]
    public int ScoutCount { get; set; } = 1;

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<Order> Orders { get; set; } = new();
    public List<BoothSale> BoothSales { get; set; } = new();
    public List<BoothInventory> Inventory { get; set; } = new();
}
