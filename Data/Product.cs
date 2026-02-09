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

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<OrderLineItem> OrderLineItems { get; set; } = new();
    public List<InventoryReceipt> InventoryReceipts { get; set; } = new();
    public List<InventoryReturn> InventoryReturns { get; set; } = new();
    public List<Payback> Paybacks { get; set; } = new();
}
