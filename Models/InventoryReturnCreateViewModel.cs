using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class InventoryReturnCreateViewModel
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityBoxes { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityCases { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    public string? Notes { get; set; }

    public List<SelectListItem> Products { get; set; } = new();
}
