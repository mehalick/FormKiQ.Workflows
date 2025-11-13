using Amazon;
using Amazon.Rekognition;
using Microsoft.Extensions.DependencyInjection;

namespace FormKiQ.Workflows.OnDocumentCreated;

public static class Startup
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddHttpClient();

        services.AddDefaultAWSOptions(new()
        {
            Region = RegionEndpoint.USEast1
        });
        services.AddAWSService<IAmazonRekognition>();

        services.AddSingleton<Handler>();
        services.AddSingleton<Processor>();

        return services.BuildServiceProvider();
    }
}
