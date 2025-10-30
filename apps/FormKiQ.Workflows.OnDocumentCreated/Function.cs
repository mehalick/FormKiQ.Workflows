using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;
using JetBrains.Annotations;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace FormKiQ.Workflows.OnDocumentCreated;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[UsedImplicitly]
public class Function
{
    static Function()
    {
        Logger.AppendKey("type", "function.log");
        Logger.LogDebug("Initializing function in static constructor");
    }

    [Logging(Service = "OnDocumentCreated")]
    [BatchProcessor(RecordHandler = typeof(Handler), BatchProcessor = typeof(Processor))]
    public static BatchItemFailuresResponse FunctionHandler(SQSEvent _)
    {
        Logger.LogDebug("Function handler complete");

        return SqsBatchProcessor.Result.BatchItemFailuresResponse;
    }
}
