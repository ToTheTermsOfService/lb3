using E_Shop_2.Helpers;
using Infrastructure.Data;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace E_Shop_2.Extensions
{
    public static class ConfigureCores
    {
        public static void ConfigureCore(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ShopDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("ShopConnection"),
                    b => b.MigrationsAssembly("API"));
            });
            services.AddAutoMapper(typeof(MappingProfiles));
            services.AddSingleton<IConnectionMultiplexer>(c =>
            {
                return ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis"));
            });
        }
    }
}
