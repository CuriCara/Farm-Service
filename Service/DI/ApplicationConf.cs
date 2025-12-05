using Service.GA.DistanceMatrix;
using Service.IoC;
using Service.Settings;

namespace Service.DI;

public class ApplicationConf
{
    public static void ConfService(WebApplicationBuilder builder, FarmSettings airTicketSettings)
    {
        DbContextConf.ConfigureService(builder);
        SerilogConf.ConfigureService(builder);
        SwaggerConf.ConfigureServices(builder.Services);
        MapperConf.ConfigureServices(builder);
        ServiceConf.ConfigureServices(builder.Services, airTicketSettings);
        AuthorizationConf.ConfigureServices(builder.Services, airTicketSettings);
        
        builder.Services.AddControllers();
    }

    public static void ConfApplication(WebApplication app)
    {
        SerilogConf.ConfigureApplication(app);
        SwaggerConf.ConfigureApplication(app);
        DbContextConf.ConfigureApplication(app);
        AuthorizationConf.ConfigureApplication(app);
        
        app.UseHttpsRedirection();
        app.MapControllers();
    }
}