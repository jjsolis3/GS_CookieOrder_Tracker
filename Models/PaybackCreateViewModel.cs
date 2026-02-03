using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GS_CookieOrder_Tracker.Models;

public class PaybackCreateViewModel
{
    [Required]
    public Guid OrderId { get; set; }

    public Guid? CustomerId { get; set; }

    [Required]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;

    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? Method { get; set; }

    public string? Notes { get; set; }

    public List<SelectListItem> Orders { get; set; } = new();
    public List<SelectListItem> Customers { get; set; } = new();
}
