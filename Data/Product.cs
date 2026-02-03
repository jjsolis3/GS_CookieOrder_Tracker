using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GS_CookieOrder_Tracker.Data;

[Table("products")]
public class Product
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [Column("sku")]
    [MaxLength(100)]
    public string? Sku { get; set; }

    [Column("price_per_box")]
    public decimal PricePerBox { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("boxes_per_case")]
    public int BoxesPerCase { get; set; }

    [Column("active")]
    public bool Active { get; set; } = true;

    [Column("image_path")]
    public string? ImagePath { get; set; }

    [Column("category")]
    public string? Category { get; set; }

    [Column("vendor")]
    public string? Vendor { get; set; }

    [Column("cost")]
    public decimal? Cost { get; set; }

    [Column("reward")]
    public decimal? Reward { get; set; }

    [Column("barcode")]
    public string? Barcode { get; set; }

    public List<OrderLineItem> OrderLineItems { get; set; } = new();
    public List<InventoryReceipt> InventoryReceipts { get; set; } = new();
}
