using AutoMapper;
using BusinessLogic.Authorization;
using BusinessLogic.Users.Manager;
using BusinessLogic.Users.Provider;
using DataAccess;
using DataAccess.Entity;
using DataAccess.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Service.Settings;

namespace Service.IoC;

public class ServiceConf
{
    public static void ConfigureServices(IServiceCollection services, FarmSettings settings)
    {
        
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IRepository<User>>(x => 
            new Repository<User>(x.GetRequiredService<FarmDbContext>()));
        services.AddScoped<IUserProvider>(x => 
            new UserProvider(x.GetRequiredService<IRepository<User>>(), 
                x.GetRequiredService<IMapper>()));
        services.AddScoped<IUserManager>(x =>
            new UserManager(x.GetRequiredService<IRepository<User>>(),
                x.GetRequiredService<IMapper>()));
        
        services.AddScoped<IAuthProvider>(x =>
            new AuthProvider(x.GetRequiredService<SignInManager<User>>(),
                x.GetRequiredService<UserManager<User>>(),
                x.GetRequiredService<IHttpClientFactory>(),
                settings.IdentityServerUri!,
                settings.ClientId!,
                settings.ClientSecret!,
                x.GetRequiredService<IMapper>()
            ));
    }
}