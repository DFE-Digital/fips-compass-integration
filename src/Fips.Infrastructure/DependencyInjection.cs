using Fips.Infrastructure.Compass;
using Fips.Infrastructure.Configuration;
using Fips.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Fips.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFipsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CompassApiOptions>(configuration.GetSection(CompassApiOptions.SectionName));
        services.Configure<FipsDataOptions>(configuration.GetSection(FipsDataOptions.SectionName));

        services.AddMemoryCache();
        services.AddHttpClient<ICompassServiceRegisterApiService, CompassServiceRegisterApiService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        return services;
    }
}
