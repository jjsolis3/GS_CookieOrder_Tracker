using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GS_CookieOrder_Tracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Example DbSets (you’ll add your real ones)
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Repayment> Repayments => Set<Repayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // If you use snake_case table/column names in Postgres, we can enable mapping conventions.
        // modelBuilder.UseSnakeCaseNamingConvention(); // requires additional package (tell me if you want this)
    }
}
