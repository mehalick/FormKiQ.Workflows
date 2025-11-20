using Amazon;
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.Textract;
using AWS.Lambda.Powertools.Logging;
using FormKiQ.Workflows.OnDocumentCreated.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FormKiQ.Workflows.OnDocumentCreated;

public static class Startup
{
    public static IServiceProvider ConfigureServices()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddEnvironmentVariables();
        var configuration = configurationBuilder.Build();

        var services = new ServiceCollection();

        services.AddHttpClient();

        services.AddLogging(builder =>
        {
            builder.AddPowertoolsLogger();
        });

        services.AddDefaultAWSOptions(new()
        {
            Region = RegionEndpoint.USEast1
        });
        services.AddAWSService<IAmazonRekognition>();
        services.AddAWSService<IAmazonS3>();
        services.AddAWSService<IAmazonTextract>();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<ImageResizer>();
        services.AddSingleton<LabelProcessor>();
        services.AddSingleton<TextProcessor>();
        services.AddSingleton<SlackNotifier>();

        services.AddSingleton<Handler>();
        services.AddSingleton<Processor>();

        return services.BuildServiceProvider();
    }
}
