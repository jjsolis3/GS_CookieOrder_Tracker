using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GS_CookieOrder_Tracker.Services;

/// <summary>
/// Wraps Supabase Auth Admin API (requires service_role key).
/// Used for: listing users, creating/inviting users, deleting users, sending password resets.
/// </summary>
public class SupabaseAdminService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceRoleKey;
    private readonly string _anonKey;

    public SupabaseAdminService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"]?.TrimEnd('/') ?? "";
        _serviceRoleKey = configuration["Supabase:ServiceRoleKey"] ?? "";
        _anonKey = configuration["Supabase:AnonKey"] ?? "";
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_supabaseUrl)
                              && !string.IsNullOrWhiteSpace(_serviceRoleKey);

    // ── List all users ──
    public async Task<List<SupabaseUserInfo>> ListUsersAsync()
    {
        var result = new List<SupabaseUserInfo>();
        if (!IsConfigured) return result;

        var request = AdminRequest(HttpMethod.Get, "/auth/v1/admin/users");
        try
        {
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return result;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            JsonElement usersArray;
            if (doc.RootElement.TryGetProperty("users", out usersArray))
            {
                foreach (var u in usersArray.EnumerateArray())
                {
                    result.Add(ParseUser(u));
                }
            }
        }
        catch { }
        return result;
    }

    // ── Create / invite a user ──
    public async Task<(bool Success, string? Error)> CreateUserAsync(string email, string? password, string role = "user")
    {
        if (!IsConfigured) return (false, "Supabase admin not configured.");

        var body = new Dictionary<string, object>
        {
            ["email"] = email,
            ["email_confirm"] = true,
            ["user_metadata"] = new { role }
        };
        if (!string.IsNullOrWhiteSpace(password))
            body["password"] = password;

        var request = AdminRequest(HttpMethod.Post, "/auth/v1/admin/users");
        request.Content = JsonContent(body);

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode) return (true, null);

            var errJson = await response.Content.ReadAsStringAsync();
            var errDoc = JsonDocument.Parse(errJson);
            var msg = errDoc.RootElement.TryGetProperty("msg", out var msgProp)
                ? msgProp.GetString()
                : errDoc.RootElement.TryGetProperty("message", out var msgProp2)
                    ? msgProp2.GetString()
                    : "Unknown error";
            return (false, msg);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // ── Update user role ──
    public async Task<bool> UpdateUserRoleAsync(string userId, string role)
    {
        if (!IsConfigured) return false;

        var body = new { user_metadata = new { role } };
        var request = AdminRequest(HttpMethod.Put, $"/auth/v1/admin/users/{userId}");
        request.Content = JsonContent(body);

        try
        {
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Delete user ──
    public async Task<bool> DeleteUserAsync(string userId)
    {
        if (!IsConfigured) return false;

        var request = AdminRequest(HttpMethod.Delete, $"/auth/v1/admin/users/{userId}");
        try
        {
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Send password reset email (uses anon key, no admin needed) ──
    public async Task<bool> SendPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(_supabaseUrl)) return false;

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_supabaseUrl}/auth/v1/recover");
        request.Headers.Add("apikey", _anonKey);
        request.Content = JsonContent(new { email });

        try
        {
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Update password using access token (for reset flow) ──
    public async Task<bool> UpdatePasswordAsync(string accessToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(_supabaseUrl)) return false;

        var request = new HttpRequestMessage(HttpMethod.Put, $"{_supabaseUrl}/auth/v1/user");
        request.Headers.Add("apikey", _anonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent(new { password = newPassword });

        try
        {
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Helpers ──
    private HttpRequestMessage AdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_supabaseUrl}{path}");
        request.Headers.Add("apikey", _serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        return request;
    }

    private static StringContent JsonContent(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }

    private static SupabaseUserInfo ParseUser(JsonElement u)
    {
        var meta = u.TryGetProperty("user_metadata", out var metaProp) ? metaProp : default;
        return new SupabaseUserInfo
        {
            Id = u.GetProperty("id").GetString() ?? "",
            Email = u.TryGetProperty("email", out var ep) ? ep.GetString() ?? "" : "",
            Role = meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty("role", out var rp)
                ? rp.GetString() ?? "user" : "user",
            CreatedAt = u.TryGetProperty("created_at", out var ca)
                ? DateTime.TryParse(ca.GetString(), out var dt) ? dt : DateTime.MinValue
                : DateTime.MinValue,
            LastSignIn = u.TryGetProperty("last_sign_in_at", out var ls) && ls.ValueKind != JsonValueKind.Null
                ? DateTime.TryParse(ls.GetString(), out var ldt) ? ldt : (DateTime?)null
                : null
        };
    }
}

public class SupabaseUserInfo
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "user";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSignIn { get; set; }
}
