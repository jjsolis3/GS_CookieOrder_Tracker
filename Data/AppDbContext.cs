using Microsoft.EntityFrameworkCore;

namespace GS_CookieOrder_Tracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<GirlScout> GirlScouts => Set<GirlScout>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLineItem> OrderLineItems => Set<OrderLineItem>();
    public DbSet<Payback> Paybacks => Set<Payback>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<InventoryReceipt> InventoryReceipts => Set<InventoryReceipt>();
    public DbSet<InventoryReturn> InventoryReturns => Set<InventoryReturn>();
    public DbSet<BoothSession> BoothSessions => Set<BoothSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>()
            .HasOne(order => order.Customer)
            .WithMany(customer => customer.Orders)
            .HasForeignKey(order => order.CustomerId);

        modelBuilder.Entity<Order>()
            .HasOne(order => order.GirlScout)
            .WithMany(scout => scout.Orders)
            .HasForeignKey(order => order.GirlScoutId);

        modelBuilder.Entity<OrderLineItem>()
            .HasOne(line => line.Order)
            .WithMany(order => order.LineItems)
            .HasForeignKey(line => line.OrderId);

        modelBuilder.Entity<OrderLineItem>()
            .HasOne(line => line.Product)
            .WithMany(product => product.OrderLineItems)
            .HasForeignKey(line => line.ProductId);

        modelBuilder.Entity<Payback>()
            .HasOne(payback => payback.Order)
            .WithMany(order => order.Paybacks)
            .HasForeignKey(payback => payback.OrderId);

        modelBuilder.Entity<Payback>()
            .HasOne(payback => payback.Customer)
            .WithMany(customer => customer.Paybacks)
            .HasForeignKey(payback => payback.CustomerId);

        modelBuilder.Entity<InventoryReceipt>()
            .HasOne(receipt => receipt.InventoryBatch)
            .WithMany(batch => batch.Receipts)
            .HasForeignKey(receipt => receipt.InventoryBatchId);

        modelBuilder.Entity<InventoryReceipt>()
            .HasOne(receipt => receipt.Product)
            .WithMany(product => product.InventoryReceipts)
            .HasForeignKey(receipt => receipt.ProductId);

        modelBuilder.Entity<InventoryBatch>()
            .HasOne(batch => batch.GirlScout)
            .WithMany(scout => scout.InventoryBatches)
            .HasForeignKey(batch => batch.GirlScoutId);

        modelBuilder.Entity<InventoryReturn>()
            .HasOne(ret => ret.Product)
            .WithMany(product => product.InventoryReturns)
            .HasForeignKey(ret => ret.ProductId);

        modelBuilder.Entity<Order>()
            .HasOne(order => order.BoothSession)
            .WithMany(session => session.Orders)
            .HasForeignKey(order => order.BoothSessionId);
    }
}
