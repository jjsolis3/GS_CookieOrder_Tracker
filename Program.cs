using GS_CookieOrder_Tracker.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("SupabaseDb");

    options.UseNpgsql(cs, npgsql =>
    {
        npgsql.EnableRetryOnFailure(5);
        npgsql.CommandTimeout(60);
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// quick health endpoint
app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
