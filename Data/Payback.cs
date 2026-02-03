using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("paybacks")]
public class Payback
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_id")]
    public Guid? OrderId { get; set; }

    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [Column("paid_at")]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("method")]
    [MaxLength(50)]
    public string? Method { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    public Order? Order { get; set; }
    public Customer? Customer { get; set; }
}
