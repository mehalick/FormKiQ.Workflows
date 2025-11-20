using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using FormKiQ.Workflows.OnDocumentCreated.Services;
using Microsoft.Extensions.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Handler : ISqsRecordHandler
{
    private readonly ILogger<Handler> _logger;
    private readonly ImageResizer _imageResizer;
    private readonly LabelProcessor _labelProcessor;
    private readonly TextProcessor  _textProcessor;
    private readonly SlackNotifier _slackNotifier;

    public Handler(ILogger<Handler> logger, ImageResizer imageResizer, LabelProcessor labelProcessor, TextProcessor textProcessor, SlackNotifier slackNotifier)
    {
        _logger = logger;
        _imageResizer = imageResizer;
        _labelProcessor = labelProcessor;
        _textProcessor = textProcessor;
        _slackNotifier = slackNotifier;
    }

    public async Task<RecordHandlerResult> HandleAsync(SQSEvent.SQSMessage record, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling SQS record {MessageId}", record.MessageId);

        try
        {
            var document = GetDocumentDetails(record.Body);

            if (document is null || document.Type != "create") // TODO: add event filtering
            {
                return RecordHandlerResult.None;
            }

            var resizeImageResult = await _imageResizer.ResizeImage(document, cancellationToken);

            if (!resizeImageResult.IsSuccess)
            {
                _logger.LogError("Error resizing image");
                return RecordHandlerResult.None;
            }

            var labels = await _labelProcessor.SaveLabels(document, resizeImageResult.S3Key, cancellationToken);
            await _textProcessor.SaveText(document, resizeImageResult.S3Key, cancellationToken);
            await _slackNotifier.SendNotification(document, resizeImageResult.S3Key, labels, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SQS record: {ErrorMessage}", ex.Message);
        }

        return RecordHandlerResult.None;
    }

    private DocumentDetails? GetDocumentDetails(string json)
    {
        var message = JsonSerializer.Deserialize(json, Serializer.Default.DocumentMessage);

        if (message is null)
        {
            _logger.LogError("Unable to deserialize document message");

            return null;
        }

        var document = JsonSerializer.Deserialize(message.Message, Serializer.Default.DocumentDetails);

        if (document is null)
        {
            _logger.LogError("Unable to deserialize document details");

            return null;
        }

        _logger.LogInformation("Document successfully deserialized {@Document}", document);

        return document;
    }
}
