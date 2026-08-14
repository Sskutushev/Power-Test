using Microsoft.Extensions.DependencyInjection;
using Weather.Application.Common;

namespace Weather.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(AssemblyMarker).Assembly);
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(PerformanceBehavior<,>));
        });
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
