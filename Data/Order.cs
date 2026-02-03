using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("orders")]
public class Order
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("order_type")]
    [MaxLength(50)]
    public string? OrderType { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string? Status { get; set; }

    [Column("ordered_at")]
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

    [Column("delivery_date")]
    public DateOnly? DeliveryDate { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("customer_id")]
    public Guid? CustomerId { get; set; }

    [Column("girl_scout_id")]
    public Guid? GirlScoutId { get; set; }

    [Column("total_price")]
    public decimal? TotalPrice { get; set; }

    [Column("total_qty")]
    public int? TotalQty { get; set; }

    [Column("paid_amount")]
    public decimal? PaidAmount { get; set; }

    public Customer? Customer { get; set; }
    public GirlScout? GirlScout { get; set; }
    public List<OrderLineItem> LineItems { get; set; } = new();
    public List<Payback> Paybacks { get; set; } = new();

    public string PaymentMethod { get; set; } = "Cash"; // or whatever default
}
