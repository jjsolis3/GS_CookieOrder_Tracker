using GS_CookieOrder_Tracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GS_CookieOrder_Tracker.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly SupabaseAdminService _adminService;

    public UsersController(SupabaseAdminService adminService)
    {
        _adminService = adminService;
    }

    // ── List users ──
    public async Task<IActionResult> Index()
    {
        if (!_adminService.IsConfigured)
        {
            ViewBag.Error = "Supabase admin is not configured. Add Supabase:ServiceRoleKey to secrets.json.";
            return View(new List<SupabaseUserInfo>());
        }

        var users = await _adminService.ListUsersAsync();
        return View(users);
    }

    // ── Invite / create user form ──
    [HttpGet]
    public IActionResult Invite()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string email, string? password, string role = "user")
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError("", "Email is required.");
            return View();
        }

        var (success, error) = await _adminService.CreateUserAsync(email.Trim(), password, role);
        if (success)
        {
            TempData["Success"] = $"User {email} created successfully.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.AddModelError("", error ?? "Failed to create user.");
        return View();
    }

    // ── Update role (AJAX) ──
    [HttpPost]
    public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest req)
    {
        var ok = await _adminService.UpdateUserRoleAsync(req.UserId, req.Role);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Failed to update role." });
    }

    // ── Delete user (AJAX) ──
    [HttpPost]
    public async Task<IActionResult> Delete([FromBody] DeleteUserRequest req)
    {
        var ok = await _adminService.DeleteUserAsync(req.UserId);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Failed to delete user." });
    }

    // ── Send password reset (AJAX) ──
    [HttpPost]
    public async Task<IActionResult> SendReset([FromBody] SendResetRequest req)
    {
        var ok = await _adminService.SendPasswordResetAsync(req.Email);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Failed to send reset email." });
    }
}

public class UpdateRoleRequest
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "user";
}

public class DeleteUserRequest
{
    public string UserId { get; set; } = "";
}

public class SendResetRequest
{
    public string Email { get; set; } = "";
}
