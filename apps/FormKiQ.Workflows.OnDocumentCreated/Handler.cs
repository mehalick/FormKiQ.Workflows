using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.SQSEvents;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Handler : ISqsRecordHandler
{
    private readonly IHttpClientFactory _httpClientFactory;

    public Handler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RecordHandlerResult> HandleAsync(SQSEvent.SQSMessage record, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Handling SQS record {MessageId}", record.MessageId);

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
                    
                    await AddDocumentLabels(documentDetails, cancellationToken);
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

    private async Task AddDocumentLabels(DocumentDetails documentDetails, CancellationToken cancellationToken)
    {
        var formKiqBaseUrl = Environment.GetEnvironmentVariable("FORMKIQ_BASE_URL");
        var formKiqApiKey = Environment.GetEnvironmentVariable("FORMKIQ_API_KEY");

        if (string.IsNullOrEmpty(formKiqBaseUrl) || string.IsNullOrEmpty(formKiqApiKey))
        {
            Logger.LogWarning("FormKiQ base URL or API key not set");
            return;
        }
        
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new(formKiqBaseUrl);
        client.DefaultRequestHeaders.Authorization = new(formKiqApiKey);

        var url = $"documents/{documentDetails.DocumentId}/attributes";

        var attributes = new LabelAttribute
        {
            StringValues = ["cat", "dog"]
        };

        var root = new AttributeList
        {
            Attributes = [attributes]
        };
        
        var response = await client.PostAsJsonAsync(url, root, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("FormKiQ attributes set {StatusCode}", response.StatusCode);

            return;
        }
        
        Logger.LogError("FormKiQ post failed {@Response}", response);
    }

    private async Task SendSlackNotification(DocumentDetails documentDetails, CancellationToken cancellationToken)
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

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(slackWebhookUrl, json, cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("Slack message sent {StatusCode}", response.StatusCode);

            return;
        }

        Logger.LogError("Slack message failed {@Response}", response);
    }

    private class LabelAttribute
    {
        [JsonPropertyName("key")]
        public string Key { get; init; } = "labels";

        [JsonPropertyName("stringValues")]
        public List<string> StringValues { get; init; } = [];
    }

    private class AttributeList
    {
        [JsonPropertyName("attributes")]
        public List<LabelAttribute> Attributes { get; init; } = [];
    }
}
