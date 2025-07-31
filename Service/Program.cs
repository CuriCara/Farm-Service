using BusinessLogic.Authorization;
using BusinessLogic.EmailSend;
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

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

var settings = FarmSettingsReader.Read(configuration);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddScoped<ProductManager>();
builder.Services.AddScoped<UnitsOfMeasurementProvider>();
builder.Services.AddScoped<UnitCategoryProvider>();
//builder.Services.AddScoped<IAuthProvider, AuthProvider>();
builder.Services.AddScoped<HarvestManager>();
builder.Services.AddScoped<ProductProvider>();
builder.Services.AddScoped<UserProvider>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<IRepository<Harvest>, HarvestProvider>();
builder.Services.AddScoped<IRepository<User>, UserProvider>();
builder.Services.AddScoped<IRepository<Product>, ProductProvider>();
builder.Services.AddScoped<IRepository<UnitsOfMeasurement>, UnitsOfMeasurementProvider>();

builder.Services.AddAutoMapper(typeof(HarvestProfile));
builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
builder.Services.AddControllers();
builder.Services.AddHostedService<ReportBackgroundService>();
builder.Services.AddDbContext<FarmDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
});

AuthorizationConf.ConfigureServices(builder.Services, settings);
DbContextConf.ConfigureService(builder);
SerilogConf.ConfigureService(builder);
SwaggerConf.ConfigureServices(builder.Services);
MapperConf.ConfigureServices(builder);
ServiceConf.ConfigureServices(builder.Services, settings);

var app = builder.Build();

SerilogConf.ConfigureApplication(app);
SwaggerConf.ConfigureApplication(app);
DbContextConf.ConfigureApplication(app);
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
