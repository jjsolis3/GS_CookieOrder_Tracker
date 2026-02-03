using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("customers")]
public class Customer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Column("email")]
    [MaxLength(200)]
    public string? Email { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    public List<Order> Orders { get; set; } = new();
    public List<Payback> Paybacks { get; set; } = new();
}
