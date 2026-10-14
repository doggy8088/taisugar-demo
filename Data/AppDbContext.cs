using Microsoft.EntityFrameworkCore;
using PropertyManagement.Models;
namespace PropertyManagement.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options) {
 public DbSet<Asset> Assets => Set<Asset>(); public DbSet<Unit> Units => Set<Unit>();
 public DbSet<Tenant> Tenants => Set<Tenant>(); public DbSet<Lease> Leases => Set<Lease>();
 public DbSet<Meter> Meters => Set<Meter>(); public DbSet<Reading> Readings => Set<Reading>();
 public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<Payment> Payments => Set<Payment>();
 public DbSet<EInvoice> EInvoices => Set<EInvoice>();
 protected override void OnModelCreating(ModelBuilder b) {
  b.Entity<Record>().UseTpcMappingStrategy();
  b.Entity<Record>().Property(x=>x.Id).ValueGeneratedNever();
  b.Entity<Asset>().ToTable("Assets"); b.Entity<Unit>().ToTable("Units"); b.Entity<Tenant>().ToTable("Tenants");
  b.Entity<Lease>().ToTable("Leases"); b.Entity<Meter>().ToTable("Meters"); b.Entity<Reading>().ToTable("Readings");
  b.Entity<Invoice>().ToTable("Invoices"); b.Entity<Payment>().ToTable("Payments"); b.Entity<EInvoice>().ToTable("EInvoices");
  b.Entity<Record>().HasIndex(x=>x.Code).IsUnique();
  b.Entity<Unit>().HasOne<Asset>().WithMany().HasForeignKey(x=>x.AssetId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Lease>().HasOne<Unit>().WithMany().HasForeignKey(x=>x.UnitId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Lease>().HasOne<Tenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Meter>().HasOne<Unit>().WithMany().HasForeignKey(x=>x.UnitId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Reading>().HasOne<Meter>().WithMany().HasForeignKey(x=>x.MeterId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Invoice>().HasOne<Lease>().WithMany().HasForeignKey(x=>x.LeaseId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Payment>().HasOne<Invoice>().WithMany().HasForeignKey(x=>x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<EInvoice>().HasOne<Invoice>().WithMany().HasForeignKey(x=>x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Invoice>().HasIndex(x=>new{x.LeaseId,x.Kind,x.Period}).IsUnique();
  b.Entity<Reading>().HasIndex(x=>new{x.MeterId,x.ReadDate}).IsUnique();
  b.Entity<EInvoice>().HasIndex(x=>x.TrackNumber).IsUnique();
  foreach(var entity in b.Model.GetEntityTypes()) foreach(var p in entity.GetProperties().Where(p=>p.ClrType==typeof(decimal)))
   p.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<decimal,long>(v=>decimal.ToInt64(v*100m),v=>v/100m));
 }
}
