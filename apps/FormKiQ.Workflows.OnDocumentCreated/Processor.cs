using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated;

internal class Processor : SqsBatchProcessor
{
    public override Task<ProcessingResult<SQSEvent.SQSMessage>> ProcessAsync(
        SQSEvent @event, IRecordHandler<SQSEvent.SQSMessage> recordHandler,
        ProcessingOptions processingOptions)
    {
        Logger.LogInformation("Processing {EventCount} record(s)", @event.Records.Count);
        return base.ProcessAsync(@event, recordHandler, processingOptions);
    }

    protected override async Task HandleRecordFailureAsync(SQSEvent.SQSMessage record, Exception exception)
    {
        Logger.LogWarning(exception, "Failed to process record {MessageId}", record.MessageId);
        await base.HandleRecordFailureAsync(record, exception);
    }
}
