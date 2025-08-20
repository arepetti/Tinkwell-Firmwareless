using Microsoft.EntityFrameworkCore;

namespace Tinkwell.Firmwareless.PublicRepository.Database;

public sealed class AppDbContext : DbContext
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Firmware> Firmwares => Set<Firmware>();
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ApiKey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Role);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Role).HasMaxLength(20);
            entity.Property(x => x.Scopes).HasMaxLength(2000);

            entity
                .HasOne(a => a.Vendor)
                .WithMany(v => v.ApiKeys)
                .HasForeignKey(a => a.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Vendor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Notes).HasMaxLength(1024);
        });

        b.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Model).HasMaxLength(200);

            entity
                .HasOne(a => a.Vendor)
                .WithMany(v => v.Products)
                .HasForeignKey(a => a.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Firmware>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Version).HasMaxLength(50);
            entity.Property(x => x.Author).HasMaxLength(50);
            entity.Property(x => x.Copyright).HasMaxLength(100);
            entity.Property(x => x.ReleaseNotesUrl).HasMaxLength(100);

            entity
                .HasOne(a => a.Product)
                .WithMany(v => v.Firmwares)
                .HasForeignKey(a => a.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
