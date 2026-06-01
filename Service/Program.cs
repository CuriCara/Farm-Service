using BusinessLogic.Authorization;
using BusinessLogic.EmailSend;
using BusinessLogic.GraphHopper.DistanceMatrix;
using BusinessLogic.Harvests.Manager;
using BusinessLogic.Harvests.Provider;
using BusinessLogic.RoleInit;
using BusinessLogic.Mapper;
using DataAccess;
using DataAccess.Entity;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Service;
using Service.IoC;
using Service.Settings;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;
var settings = FarmSettingsReader.Read(configuration);

builder.Services.AddAutoMapper(typeof(HarvestProfile));
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddControllers();
builder.Configuration.AddUserSecrets<Program>();
builder.Services.AddHostedService<ReportBackgroundService>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddHttpClient<IDistanceMatrixProvider, GraphHopperElementWiseMatrixProvider>();
AuthorizationConf.ConfigureServices(builder.Services, settings);
DbContextConf.ConfigureService(builder);
SerilogConf.ConfigureService(builder);
SwaggerConf.ConfigureServices(builder.Services);
MapperConf.ConfigureServices(builder);
ServiceConf.ConfigureServices(builder.Services, settings);
ProviderConf.ConfigureServices(builder);

var app = builder.Build();

SerilogConf.ConfigureApplication(app);
SwaggerConf.ConfigureApplication(app);
await DbContextConf.ConfigureApplicationAsync(app);
AuthorizationConf.ConfigureApplication(app);

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await RoleInitializer.SeedRolesAndAdminAsync(services);
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapRazorPages();
app.MapFallbackToPage("/Index");
app.MapPost("/send-test-report", async (HttpContext context) =>
{
    var reportService = context.RequestServices.GetRequiredService<ReportService>();
    await reportService.GenerateDailyReportAsync();
    return Results.Ok("Отчет отправлен!");
});

app.Run();
