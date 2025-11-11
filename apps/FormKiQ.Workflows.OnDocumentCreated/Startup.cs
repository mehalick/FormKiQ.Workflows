using Microsoft.Extensions.DependencyInjection;

namespace FormKiQ.Workflows.OnDocumentCreated;

public static class Startup
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddHttpClient();
        services.AddSingleton<Handler>();
        services.AddSingleton<Processor>();

        return services.BuildServiceProvider();
    }
}
