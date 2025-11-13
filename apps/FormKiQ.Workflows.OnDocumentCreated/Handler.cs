using System.Net.Http.Json;
using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;
using FormKiQ.Workflows.OnDocumentCreated.Models;

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
            var document = GetDocumentDetails(record.Body);

            if (document is not null)
            {
                var labels = await GetDocumentLabels(document, cancellationToken);
                await SetDocumentLabels(document, labels, cancellationToken);
                await SendSlackNotification(document, labels, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing SQS record: {ErrorMessage}", ex.Message);
        }

        return RecordHandlerResult.None;
    }
    
    private static DocumentDetails? GetDocumentDetails(string json)
    {
        var message = JsonSerializer.Deserialize(json, Serializer.Default.DocumentMessage);

        if (message is null)
        {
            Logger.LogError("Unable to deserialize document message");
                
            return null;
        }

        var document = JsonSerializer.Deserialize(message.Message, Serializer.Default.DocumentDetails);

        if (document is null)
        {
            Logger.LogError("Unable to deserialize document details");
                
            return null;
        }

        Logger.LogInformation("Document successfully deserialized {@Document}", document);
        
        return document;
    }

    private async Task<List<string>> GetDocumentLabels(DocumentDetails documentDetails, CancellationToken cancellationToken)
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
                return [];
            }

            foreach (var label in detectLabelResponse.Labels)
            {
                Logger.LogInformation("Detecting label {@Label}", label);
                
                labels.Add(label.Name);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error using Rekognition: {ErrorMessage}", ex.Message);
        }

        return labels;
    }

    private async Task SetDocumentLabels(DocumentDetails document, List<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0)
        {
            return;
        }
        
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

        var url = $"documents/{document.DocumentId}/attributes";
        var attributes = LabelAttributeList.Create(labels);
        var response = await client.PostAsJsonAsync(url, attributes, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("FormKiQ attributes set {StatusCode}", response.StatusCode);
        }
        else
        {
            Logger.LogError("FormKiQ post failed {@Response}", response);
        }
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
        }
        else
        {
            Logger.LogError("Slack message failed {@Response}", response);
        }
    }
}
