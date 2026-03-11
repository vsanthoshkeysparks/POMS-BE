using Microsoft.EntityFrameworkCore;
using POManagement.API.Models;

namespace POManagement.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<LineItem> LineItems => Set<LineItem>();
    public DbSet<ApprovalLog> ApprovalLogs => Set<ApprovalLog>();
    public DbSet<MaterialReceipt> MaterialReceipts => Set<MaterialReceipt>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PurchaseOrder -> LineItems (Cascade Delete)
        modelBuilder.Entity<LineItem>()
            .HasOne(li => li.PurchaseOrder)
            .WithMany(po => po.LineItems)
            .HasForeignKey(li => li.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // PurchaseOrder -> ApprovalLogs (Cascade Delete)
        modelBuilder.Entity<ApprovalLog>()
            .HasOne(al => al.PurchaseOrder)
            .WithMany(po => po.ApprovalLogs)
            .HasForeignKey(al => al.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // PurchaseOrder -> MaterialReceipt (One-to-One)
        modelBuilder.Entity<MaterialReceipt>()
            .HasOne(mr => mr.PurchaseOrder)
            .WithOne(po => po.MaterialReceipt)
            .HasForeignKey<MaterialReceipt>(mr => mr.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // PurchaseOrder -> Payment (One-to-One)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.PurchaseOrder)
            .WithOne(po => po.Payment)
            .HasForeignKey<Payment>(p => p.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index on PONumber for fast lookup
        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(po => po.PONumber)
            .IsUnique();
    }
}
