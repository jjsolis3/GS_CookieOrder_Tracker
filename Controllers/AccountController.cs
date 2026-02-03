using System.Security.Claims;
using GS_CookieOrder_Tracker.Models;
using GS_CookieOrder_Tracker.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace GS_CookieOrder_Tracker.Controllers;

public class AccountController : Controller
{
    private readonly SupabaseAuthService _authService;
    private readonly IConfiguration _configuration;

    public AccountController(SupabaseAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Try Supabase Auth first
        var supabaseUser = await _authService.SignInAsync(model.Username, model.Password);

        if (supabaseUser != null)
        {
            await SignInCookie(supabaseUser.Email, supabaseUser.Id, model.RememberMe);
            return RedirectToLocal(returnUrl);
        }

        // 2. Fallback: check local admin credentials from config
        var adminUsername = _configuration["AdminUser:Username"];
        var adminPassword = _configuration["AdminUser:Password"];

        if (!string.IsNullOrWhiteSpace(adminUsername) && !string.IsNullOrWhiteSpace(adminPassword) &&
            string.Equals(model.Username, adminUsername, StringComparison.OrdinalIgnoreCase) &&
            model.Password == adminPassword)
        {
            await SignInCookie(adminUsername, "local-admin", model.RememberMe);
            return RedirectToLocal(returnUrl);
        }

        // Neither worked
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInCookie(string name, string userId, bool persistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }
}
