using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class OrderCreateViewModel
{
    [Required]
    public Guid CustomerId { get; set; }

    public Guid? GirlScoutId { get; set; }

    [Required]
    [MaxLength(50)]
    public string OrderType { get; set; } = "";

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "";

    [Required]
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

    public DateOnly? DeliveryDate { get; set; }

    public string? Notes { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int QuantityBoxes { get; set; } = 1;

    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> GirlScouts { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
}
