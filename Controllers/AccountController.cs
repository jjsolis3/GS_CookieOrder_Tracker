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
    private readonly SupabaseAdminService _adminService;
    private readonly IConfiguration _configuration;

    public AccountController(
        SupabaseAuthService authService,
        SupabaseAdminService adminService,
        IConfiguration configuration)
    {
        _authService = authService;
        _adminService = adminService;
        _configuration = configuration;
    }

    // ── Login ──
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
            return View(model);

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

    // ── Forgot Password ──
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ViewBag.Error = "Email is required.";
            return View();
        }

        await _adminService.SendPasswordResetAsync(email.Trim());

        // Always show success (don't reveal if email exists)
        ViewBag.Success = true;
        ViewBag.Email = email.Trim();
        return View();
    }

    // ── Reset Password ──
    // Supabase redirects to: https://yoursite.com/#access_token=...&type=recovery
    // The hash fragment isn't sent to the server, so we use a client-side page
    // that reads the token from the URL and submits the new password via AJAX.
    [HttpGet]
    public IActionResult ResetPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.AccessToken) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { error = "Token and password are required." });

        if (req.NewPassword.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });

        var ok = await _adminService.UpdatePasswordAsync(req.AccessToken, req.NewPassword);
        return ok
            ? Ok(new { success = true })
            : BadRequest(new { error = "Failed to reset password. The link may have expired." });
    }

    // ── Helpers ──
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

public class ResetPasswordRequest
{
    public string AccessToken { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
