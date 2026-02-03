using System.ComponentModel.DataAnnotations;

namespace GS_CookieOrder_Tracker.Models;

public class GirlScoutCreateViewModel
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = "";

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = "";

    [MaxLength(50)]
    public string? TroopNumber { get; set; }
}
