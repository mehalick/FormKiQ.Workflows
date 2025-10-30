using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Handler : ISqsRecordHandler
{
    public async Task<RecordHandlerResult> HandleAsync(SQSEvent.SQSMessage record, CancellationToken cancellationToken)
    {
        Logger.LogInformation($"Handling SQS record {record.MessageId}");

        try
        {
            var documentMessage = JsonSerializer.Deserialize(record.Body, Serializer.Default.DocumentMessage);

            if (documentMessage is null)
            {
                Logger.LogError("Unable to deserialize document message");
            }
            else
            {
                var documentDetails = JsonSerializer.Deserialize(documentMessage.Message, Serializer.Default.DocumentDetails);

                if (documentDetails is null)
                {
                    Logger.LogError("Unable to deserialize document details");
                }
                else
                {
                    Logger.LogInformation("Document successfully deserialized {@Document}", documentDetails);

                    await SendSlackNotification(documentDetails, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }

        return await Task.FromResult(RecordHandlerResult.None);
    }

    private static async Task SendSlackNotification(DocumentDetails documentDetails, CancellationToken cancellationToken)
    {
        var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            Logger.LogWarning("Slack webhook URL is not set");
            return;
        }

        var json = new
        {
            text = $"New document <{documentDetails.Url}|{documentDetails.DocumentId}> uploaded"
        };

        var client = new HttpClient();
        var response = await client.PostAsJsonAsync(slackWebhookUrl, json, cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("Slack message sent {StatusCode}", response.StatusCode);

            return;
        }

        Logger.LogError("Slack message failed {@Response}", response);
    }
}
