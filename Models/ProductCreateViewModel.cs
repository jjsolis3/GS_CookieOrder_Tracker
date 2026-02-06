using System.ComponentModel.DataAnnotations;

namespace GS_CookieOrder_Tracker.Models;

public class ProductCreateViewModel
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    [MaxLength(100)]
    public string? Sku { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price per box must be greater than zero.")]
    public decimal PricePerBox { get; set; }

    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Boxes per case must be at least 1.")]
    public int BoxesPerCase { get; set; } = 12;

    public bool Active { get; set; } = true;

    public string? ImagePath { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Vendor { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Reward { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; } = 0;
}
