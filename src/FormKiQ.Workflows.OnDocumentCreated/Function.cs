using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.Logging;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace FormKiQ.Workflows.OnDocumentCreated;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[UsedImplicitly]
public class Function
{
    private static readonly IServiceProvider _serviceProvider;
    private readonly Handler _handler = _serviceProvider.GetRequiredService<Handler>();
    private readonly Processor _processor = _serviceProvider.GetRequiredService<Processor>();

    static Function()
    {
        Logger.AppendKey("type", "function.log");
        Logger.LogDebug("Initializing function in static constructor");

        _serviceProvider = Startup.ConfigureServices();
    }

    [Logging(Service = "OnDocumentCreated")]
    public async Task<BatchItemFailuresResponse> FunctionHandler(SQSEvent sqsEvent)
    {
        Logger.LogDebug("Function handler started");

        var result = await _processor.ProcessAsync(sqsEvent, _handler);

        Logger.LogDebug("Function handler completed");

        return result.BatchItemFailuresResponse;
    }
}
