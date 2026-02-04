using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class PaybackCreateViewModel
{
    [Required]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    [Required]
    [MaxLength(50)]
    public string Method { get; set; } = "Check";

    public string? Notes { get; set; }

    // Dropdown options
    public List<SelectListItem> Orders { get; set; } = new();
    public List<SelectListItem> PaymentMethods { get; set; } = new();

    // Order amounts for JS auto-fill (OrderId -> TotalPrice)
    public Dictionary<string, decimal> OrderAmounts { get; set; } = new();
}
