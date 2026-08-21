using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ERP.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AppPage> AppPages => Set<AppPage>();
    public DbSet<RolePage> RolePages => Set<RolePage>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<POHeader> POHeaders => Set<POHeader>();
    public DbSet<POLine> POLines => Set<POLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppPage>().HasKey(p => p.PageId);
        modelBuilder.Entity<POHeader>().HasKey(h => h.POId);
        // composite keys for the junction tables
        modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
        modelBuilder.Entity<RolePage>().HasKey(rp => new { rp.RoleId, rp.PageId });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.UserName).IsUnique();
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.RoleName).IsUnique();
        modelBuilder.Entity<AppPage>()
            .HasIndex(p => p.PageCode).IsUnique();
        modelBuilder.Entity<POHeader>()
            .HasIndex(p => p.PONumber).IsUnique();

        modelBuilder.Entity<POLine>()
            .Property(l => l.Amount).HasPrecision(18, 2);
        modelBuilder.Entity<POLine>()
            .Property(l => l.Qty).HasPrecision(18, 2);
        modelBuilder.Entity<POLine>()
            .Property(l => l.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<POHeader>()
            .Property(h => h.TotalQty).HasPrecision(18, 2);
        modelBuilder.Entity<POHeader>()
            .Property(h => h.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Item>()
            .Property(i => i.DefaultPurchasePrice).HasPrecision(18, 2);
    }
}