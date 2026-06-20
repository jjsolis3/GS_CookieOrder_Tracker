using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class InventoryBatchCreateViewModel
{
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Received";

    [Required]
    [MaxLength(50)]
    public string BatchType { get; set; } = "Initial Order";

    public DateOnly? PickupDate { get; set; }

    public string? Notes { get; set; }

    public Guid? GirlScoutId { get; set; }

    /// <summary>Multiple receipt lines per batch.</summary>
    public List<ReceiptLineViewModel> Lines { get; set; } = new();

    // -- Dropdown options (not bound on POST) --
    public List<SelectListItem> GirlScouts { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
    public List<SelectListItem> Statuses { get; set; } = new();
    public List<SelectListItem> BatchTypes { get; set; } = new();
    public List<ProductCardItem> ProductCards { get; set; } = new();
}

/// <summary>Reusable product card data for visual product selection UI.</summary>
public class ProductCardItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public decimal PricePerBox { get; set; }
    public int BoxesPerCase { get; set; }
    public string? ImagePath { get; set; }
}

public class ReceiptLineViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityBoxes { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityCases { get; set; }
}
