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
    public string OrderType { get; set; } = "Direct Sale";

    [Required]
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

    public DateOnly? DeliveryDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>Multiple line items for this order.</summary>
    public List<OrderLineItemViewModel> LineItems { get; set; } = new();

    // -- Select-list options (not bound on POST) --
    public List<SelectListItem> Customers { get; set; } = new();
    public List<SelectListItem> GirlScouts { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
    public List<SelectListItem> OrderTypes { get; set; } = new();
    public List<SelectListItem> InventorySources { get; set; } = new();
}

public class OrderLineItemViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
    public int QuantityBoxes { get; set; } = 1;

    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public decimal UnitPrice { get; set; }

    [MaxLength(20)]
    public string InventorySource { get; set; } = "Personal";
}
