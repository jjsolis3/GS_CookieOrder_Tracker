using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GS_CookieOrder_Tracker.Services;

public class SupabaseAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _supabaseAnonKey;

    public SupabaseAuthService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _supabaseUrl = configuration["Supabase:Url"]?.TrimEnd('/') ?? "";
        _supabaseAnonKey = configuration["Supabase:AnonKey"] ?? "";
    }

    /// <summary>
    /// Authenticate a user via Supabase Auth (GoTrue) using email + password.
    /// Returns the user info on success, or null on failure.
    /// </summary>
    public async Task<SupabaseUser?> SignInAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(_supabaseUrl) || string.IsNullOrWhiteSpace(_supabaseAnonKey))
            return null;

        var url = $"{_supabaseUrl}/auth/v1/token?grant_type=password";

        var payload = JsonSerializer.Serialize(new { email, password });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;
        request.Headers.Add("apikey", _supabaseAnonKey);

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var user = root.GetProperty("user");
            return new SupabaseUser
            {
                Id = user.GetProperty("id").GetString() ?? "",
                Email = user.GetProperty("email").GetString() ?? "",
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                Role = user.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null
            };
        }
        catch
        {
            return null;
        }
    }
}

public class SupabaseUser
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string? Role { get; set; }
}
