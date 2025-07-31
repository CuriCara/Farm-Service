using BusinessLogic.Mapper;
using Service.Mapper;

public static class MapperConf
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddAutoMapper(config =>
        {
            config.AddProfile<UsersBLProfile>();
            config.AddProfile<UsersServiceProfile>();
            config.AddProfile<HarvestProfile>();
        });
    }
}