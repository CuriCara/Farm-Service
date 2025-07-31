using DataAccess;
using DataAccess.Entity;
using Duende.IdentityServer.Models;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

using Service.Settings;

namespace Service.IoC;

public class AuthorizationConf
{
    public static void ConfigureServices(IServiceCollection services, FarmSettings settings)
    {
        IdentityModelEventSource.ShowPII = true;

        services.AddIdentity<User, Role>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
            })
            .AddEntityFrameworkStores<FarmDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddIdentityServer()
            .AddInMemoryApiScopes(new[] { new ApiScope("api") })
            .AddInMemoryClients(new[]
            {
                new Client
                {
                    ClientId = settings.ClientId!,
                    ClientName = settings.ClientId,
                    Enabled = true,
                    AllowOfflineAccess = true,
                    AllowedGrantTypes =
                    {
                        GrantType.ClientCredentials,
                        GrantType.ResourceOwnerPassword,
                    },
                    ClientSecrets =
                    {
                        new Secret(settings.ClientSecret!.Sha256())
                    },
                    AllowedScopes = { "api" }
                }
            })
            .AddAspNetIdentity<User>();
        
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
        });
        
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        });
        
        services.AddAuthorization();
    }

    public static void ConfigureApplication(IApplicationBuilder app)
    {
        app.UseCors("AllowAll");

        app.UseIdentityServer();

        app.UseAuthentication();
        app.UseAuthorization();
    }
}
