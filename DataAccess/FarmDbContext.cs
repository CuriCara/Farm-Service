using DataAccess.Entity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DataAccess;

public class FarmDbContext : IdentityDbContext<User, Role, int>
{
    public DbSet<UserRating> UserRatings { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<UnitsOfMeasurement> UnitsOfMeasurements { get; set; }
    public DbSet<Harvest> Harvests { get; set; }
    public DbSet<UnitCategory> UnitCategories { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<HarvestQuota> HarvestQuotas { get; set; }


    public FarmDbContext(DbContextOptions<FarmDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRating>()
            .HasOne(u => u.User)
            .WithMany(ur => ur.UserRatings)
            .HasForeignKey(u => u.UserId);

        modelBuilder.Entity<Harvest>()
            .HasOne(p => p.Product)
            .WithMany(h => h.Harvests)
            .HasForeignKey(p => p.ProductId);

        modelBuilder.Entity<Harvest>()
            .HasOne(u => u.User)
            .WithMany(h => h.Harvests)
            .HasForeignKey(u => u.UserId);

        modelBuilder.Entity<Report>()
            .HasOne(h => h.Harvest)
            .WithMany(r => r.Reports)
            .HasForeignKey(h => h.HarvestId);
        
        modelBuilder.Entity<HarvestQuota>()
            .HasIndex(q => q.Date)
            .IsUnique();
        
        modelBuilder.Entity<UnitCategory>()
            .HasOne(uc => uc.BaseUnit)
            .WithMany()
            .HasForeignKey(uc => uc.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        
        modelBuilder.Entity<UnitsOfMeasurement>()
            .HasOne(u => u.Category)
            .WithMany(c => c.Units)
            .HasForeignKey(u => u.UnitCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.UnitCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}