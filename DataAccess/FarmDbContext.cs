using DataAccess.Entity;
using DataAccess.Entity.Logistics.GA.Runs;
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
    public DbSet<Farm> Farms { get; set; }
    public DbSet<Store> Stores { get; set; }
    public DbSet<StoreProduct> StoreProducts { get; set; }
    public DbSet<DeliveryPlan> DeliveryPlans { get; set; }
    public DbSet<DeliveryItem> DeliveryItems { get; set; }

    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<FarmStorage> FarmStorages { get; set; }
    public DbSet<StoreDemand> StoreDemands { get; set; }
    public DbSet<RoutePlan> RoutePlans { get; set; }
    public DbSet<Route> Routes { get; set; }
    public DbSet<RouteStop> RouteStops { get; set; }
    
    public DbSet<OptimizationRun> OptimizationRuns { get; set; }
    public DbSet<FitnessHistoryPoint> FitnessHistoryPoints { get; set; }
    public DbSet<OptimizationRoute> OptimizationRoutes { get; set; }

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

        modelBuilder.Entity<Harvest>()
            .HasOne(h => h.Farm)
            .WithMany(f => f.Harvests)
            .HasForeignKey(h => h.FarmId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StoreProduct>()
            .HasOne(sp => sp.Store)
            .WithMany(s => s.Products)
            .HasForeignKey(sp => sp.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StoreProduct>()
            .HasOne(sp => sp.Product)
            .WithMany()
            .HasForeignKey(sp => sp.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryPlan>()
            .HasOne(dp => dp.Store)
            .WithMany(s => s.DeliveryPlans)
            .HasForeignKey(dp => dp.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryPlan>()
            .HasMany(dp => dp.Items)
            .WithOne(di => di.DeliveryPlan)
            .HasForeignKey(di => di.DeliveryPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Vehicle>()
            .HasOne<Farm>()
            .WithMany()
            .HasForeignKey(v => v.StartPointId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<FarmStorage>()
            .HasOne(fs => fs.Farm)
            .WithMany(f => f.Storages)
            .HasForeignKey(fs => fs.FarmId);

        modelBuilder.Entity<FarmStorage>()
            .HasOne(fs => fs.Product)
            .WithMany()
            .HasForeignKey(fs => fs.ProductId);

        modelBuilder.Entity<StoreDemand>()
            .HasOne(sd => sd.Store)
            .WithMany(s => s.Demands)
            .HasForeignKey(sd => sd.StoreId);

        modelBuilder.Entity<StoreDemand>()
            .HasOne(sd => sd.Product)
            .WithMany()
            .HasForeignKey(sd => sd.ProductId);

        modelBuilder.Entity<StoreDemand>()
            .HasIndex(sd => new { sd.StoreId, sd.ProductId, sd.Date })
            .IsUnique();

        
        // Связи для путей оптимизации 
        modelBuilder.Entity<RoutePlan>()
            .HasMany(rp => rp.Routes)
            .WithOne(r => r.RoutePlan)
            .HasForeignKey(r => r.RoutePlanId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<RouteStop>()
            .HasOne(rs => rs.Farm)
            .WithMany(f => f.RouteStops)
            .HasForeignKey(rs => rs.FarmId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RouteStop>()
            .HasOne(rs => rs.Store)
            .WithMany(s => s.RouteStops)
            .HasForeignKey(rs => rs.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RouteStop>()
            .HasOne(rs => rs.Route)
            .WithMany(r => r.Stops)
            .HasForeignKey(rs => rs.RouteId);
        
        modelBuilder.Entity<RouteStop>()
            .HasCheckConstraint("CK_RouteStop_Location",
                "((\"LocationType\" = 0 AND \"FarmId\" IS NOT NULL AND \"StoreId\" IS NULL) OR " +
                "(\"LocationType\" = 1 AND \"StoreId\" IS NOT NULL AND \"FarmId\" IS NULL) OR " +
                "(\"LocationType\" = 2 AND \"FarmId\" IS NOT NULL AND \"StoreId\" IS NULL))");

        modelBuilder.Entity<Route>()
            .HasOne(r => r.Vehicle)
            .WithMany()
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.SetNull);
        
        modelBuilder.Entity<Route>()
            .HasOne(r => r.Depot)
            .WithMany()
            .HasForeignKey(r => r.DepotId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Route_Depot");

        modelBuilder.Entity<Route>()
            .HasMany(r => r.Stops)
            .WithOne(s => s.Route)
            .HasForeignKey(s => s.RouteId)
            .OnDelete(DeleteBehavior.Cascade);
        
        
        // Настройки связей для оптизированных ранов 
        modelBuilder.Entity<OptimizationRun>()
            .HasMany(r => r.Routes)
            .WithOne(r => r.Run)
            .HasForeignKey(r => r.OptimizationRunId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<OptimizationRun>()
            .HasMany(r => r.FitnessHistory)
            .WithOne(f => f.Run)
            .HasForeignKey(f => f.OptimizationRunId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<OptimizationRoute>()
            .Property(r => r.DistanceKm)
            .IsRequired();

        modelBuilder.Entity<OptimizationRoute>()
            .Property(r => r.TimeHours)
            .IsRequired();
        
        modelBuilder.Entity<FitnessHistoryPoint>()
            .HasKey(f => f.Id);

        modelBuilder.Entity<FitnessHistoryPoint>()
            .HasIndex(f => new { f.OptimizationRunId, f.Generation })
            .IsUnique();
        
        modelBuilder.Entity<OptimizationRun>()
            .HasIndex(r => r.PlanningDate);

        modelBuilder.Entity<OptimizationRun>()
            .HasIndex(r => r.BestFitness);
        
        modelBuilder.Entity<OptimizationRun>()
            .Property(r => r.FitnessObjective)
            .HasConversion<string>();
    }
}
