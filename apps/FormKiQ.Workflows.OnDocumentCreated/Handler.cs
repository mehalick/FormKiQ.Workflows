using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.SQSEvents;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Handler : ISqsRecordHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonRekognition _rekognitionClient;

    public Handler(IHttpClientFactory httpClientFactory, IAmazonRekognition rekognitionClient)
    {
        _httpClientFactory = httpClientFactory;
        _rekognitionClient = rekognitionClient;
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
                    
                    var labels = await AddDocumentLabels(documentDetails, cancellationToken);
                    await SendSlackNotification(documentDetails, labels, cancellationToken);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
        }

        return await Task.FromResult(RecordHandlerResult.None);
    }

    private async Task<List<string>> AddDocumentLabels(DocumentDetails documentDetails, CancellationToken cancellationToken)
    {
        var labels = new List<string>();
        
        try
        {
            var request = new DetectLabelsRequest
            {
                Image = new()
                {
                    S3Object = new()
                    {
                        Bucket = documentDetails.S3Bucket,
                        Name = documentDetails.S3Key
                    }
                },
                MaxLabels = 10,
                MinConfidence = 75F
            };
        
            var detectLabelResponse = await _rekognitionClient.DetectLabelsAsync(request, cancellationToken);
            
            if (detectLabelResponse.Labels.Count == 0)
            {
                Logger.LogInformation("No labels detected");
                return labels;
            }

            foreach (var label in detectLabelResponse.Labels)
            {
                Logger.LogInformation("Detecting label {@Label}", label);
                Logger.LogInformation("Detecting label {Label} {Confidence}", label.Name, label.Confidence);
                
                labels.Add(label.Name);
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, "Error using Rekognition");
        }
        
        var formKiqBaseUrl = Environment.GetEnvironmentVariable("FORMKIQ_BASE_URL");
        var formKiqApiKey = Environment.GetEnvironmentVariable("FORMKIQ_API_KEY");

        if (string.IsNullOrEmpty(formKiqBaseUrl) || string.IsNullOrEmpty(formKiqApiKey))
        {
            Logger.LogWarning("FormKiQ base URL or API key not set");
            return labels;
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new(formKiqBaseUrl);
        client.DefaultRequestHeaders.Authorization = new(formKiqApiKey);

        var url = $"documents/{documentDetails.DocumentId}/attributes";

        var attributes = new AttributeList
        {
            Attributes = [new()
            {
                StringValues = labels
            }]
        };
        
        var response = await client.PostAsJsonAsync(url, attributes, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("FormKiQ attributes set {StatusCode}", response.StatusCode);
        }
        else
        {
            Logger.LogError("FormKiQ post failed {@Response}", response);
        }

        return labels;
    }

    private async Task SendSlackNotification(DocumentDetails documentDetails, List<string> labels, CancellationToken cancellationToken)
    {
        var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            Logger.LogWarning("Slack webhook URL is not set");
            return;
        }

        var labelList = string.Join(", ", labels);

        var json = new
        {
            text = $"New document <{documentDetails.Url}|{documentDetails.Path}> uploaded, labels: {labelList}"
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
