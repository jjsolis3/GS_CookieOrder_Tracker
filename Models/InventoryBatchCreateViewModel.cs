using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class InventoryBatchCreateViewModel
{
    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? BatchType { get; set; }

    public DateOnly? PickupDate { get; set; }

    public string? Notes { get; set; }

    public Guid? GirlScoutId { get; set; }

    [Required]
    public Guid ProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityBoxes { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityCases { get; set; }

    public List<SelectListItem> GirlScouts { get; set; } = new();
    public List<SelectListItem> Products { get; set; } = new();
}
