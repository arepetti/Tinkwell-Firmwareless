using Microsoft.EntityFrameworkCore;

namespace Tinkwell.Firmwareless.PublicRepository.Database;

public sealed class AppDbContext : DbContext
{
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
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

        b.Entity<Vendor>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
        });
    }
}
