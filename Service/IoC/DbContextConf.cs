using Service.Settings;

namespace Service.IoC;

using Microsoft.EntityFrameworkCore;
using DataAccess;

public class DbContextConf
{
    public static void ConfigureService(WebApplicationBuilder builder)
    {
        var connectString = builder.Configuration.GetConnectionString("FarmDbContext");

        if (string.IsNullOrEmpty(connectString))
        {
            throw new InvalidOperationException("Connection string 'FarmDbContext' not found.");
        }

        builder.Services.AddDbContextFactory<FarmDbContext>(options =>
        {
            options.UseNpgsql(connectString);
        });
    }

    public static async Task ConfigureApplicationAsync(IApplicationBuilder app)
    {
        
        using var scope = app.ApplicationServices.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FarmDbContext>>();
        using var context = contextFactory.CreateDbContext();
        // Лучше делать миграции асинхронно
        await context.Database.MigrateAsync();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        
        var osmConn = config.GetConnectionString("OsmConnection");
        
        await FarmDbInitializer.Seed(context);
        await FarmDbInitializer.CreateEntityAsync(context, osmConn);
    }   
}