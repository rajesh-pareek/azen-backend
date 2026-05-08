using Azen.Infrastructure.Services;
using Azen.Application.Interfaces;
using Azen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Azen.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("AuthDb")));
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("AppDb")));

        var useRealSMSServiceForOTP = bool.TryParse(configuration["FeatureFlags:UseRealSMS"], out var val) && val;
        Console.WriteLine($"real SMs feature flags : {useRealSMSServiceForOTP}");
        if (useRealSMSServiceForOTP)
        {
            /* services.AddHttpClient<ISmsService, RealSmsService>(); */
            services.AddHttpClient<ISmsService, RealSmsService>();
        }
        else
        {
            services.AddScoped<ISmsService, ConsoleSmsService>();
        }
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IShipmentRefService, ShipmentRefService>();
        services.AddSingleton<IStorageService, S3StorageService>();

        return services;
    }
}

