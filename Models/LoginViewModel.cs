using System.ComponentModel.DataAnnotations;

namespace GS_CookieOrder_Tracker.Models;

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Username { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; }
}
