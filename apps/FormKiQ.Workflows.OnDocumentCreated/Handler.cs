using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using AWS.Lambda.Powertools.BatchProcessing;
using AWS.Lambda.Powertools.BatchProcessing.Sqs;
using AWS.Lambda.Powertools.Logging;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Document = Amazon.Textract.Model.Document;

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Handler : ISqsRecordHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonRekognition _rekognitionClient;
    private readonly IAmazonTextract _textractClient;

    public Handler(IHttpClientFactory httpClientFactory, IAmazonRekognition rekognitionClient, IAmazonTextract textractClient)
    {
        _httpClientFactory = httpClientFactory;
        _rekognitionClient = rekognitionClient;
        _textractClient = textractClient;
    }

    public async Task<RecordHandlerResult> HandleAsync(SQSEvent.SQSMessage record, CancellationToken cancellationToken)
    {
        Logger.LogInformation("<Handler> Handling SQS record {MessageId}", record.MessageId);

        try
        {
            var document = GetDocumentDetails(record.Body);

            if (document is not null && document.Type == "create")
            {
                    var labels = await GetDocumentLabels(document, cancellationToken);
                    var words = await GetDocumentText(document, cancellationToken);
                    await SetDocumentLabels(document, labels, cancellationToken);
                    await SendSlackNotification(document, labels, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "<Handler> Error processing SQS record: {ErrorMessage}", ex.Message);
        }

        return RecordHandlerResult.None;
    }

    private static DocumentDetails? GetDocumentDetails(string json)
    {
        var message = JsonSerializer.Deserialize(json, Serializer.Default.DocumentMessage);

        if (message is null)
        {
            Logger.LogError("<Handler> Unable to deserialize document message");

            return null;
        }

        var document = JsonSerializer.Deserialize(message.Message, Serializer.Default.DocumentDetails);

        if (document is null)
        {
            Logger.LogError("<Handler> Unable to deserialize document details");

            return null;
        }

        Logger.LogInformation("<Handler> Document successfully deserialized {@Document}", document);

        return document;
    }

    private async Task<List<string>> GetDocumentLabels(DocumentDetails document, CancellationToken cancellationToken)
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
                        Bucket = document.S3Bucket,
                        Name = document.S3Key
                    }
                },
                MaxLabels = 10,
                MinConfidence = 75F
            };

            var response = await _rekognitionClient.DetectLabelsAsync(request, cancellationToken);

            if (response.Labels.Count == 0)
            {
                Logger.LogInformation("<Handler> No labels detected");
                return [];
            }

            foreach (var label in response.Labels)
            {
                Logger.LogDebug("<Handler> Detecting label {@Label}", label);

                labels.Add(label.Name);
            }

            Logger.LogInformation("<Handler> Labels detected {@Labels}", labels);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "<Handler> Error using Rekognition: {ErrorMessage}", ex.Message);
        }

        return labels;
    }

    private async Task<string> GetDocumentText(DocumentDetails document, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _textractClient.DetectDocumentTextAsync(new()
            {
                Document = new()
                {
                    S3Object = new()
                    {
                        Bucket = document.S3Bucket,
                        Name = document.S3Key
                    }
                }
            }, cancellationToken);

            Logger.LogInformation("<Handler> Textract complete {StatusCode}", response.HttpStatusCode);

            var words = response.Blocks
                .Where(b => b.BlockType == BlockType.WORD)
                .Select(w => w.Text)
                .ToList();

            Logger.LogInformation("<Handler> Textract complete {StatusCode}, {Count} words found", response.HttpStatusCode, words.Count);
            Logger.LogInformation("<Handler> Textract: {Words}", string.Join(", ", words));

            return string.Join(' ', words);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "<Handler> Error using Textract: {ErrorMessage}", ex.Message);
            return "";
        }
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
            Logger.LogWarning("<Handler> FormKiQ base URL or API key not set");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new(formKiqBaseUrl);
        client.DefaultRequestHeaders.Authorization = new(formKiqApiKey);

        var url = $"documents/{document.DocumentId}/attributes";
        var attributes = LabelAttributeList.Create(labels);

        Logger.LogInformation("<Handler> Sending attributes {@Attributes}", new
        {
            Url = url,
            ApiKey = formKiqApiKey,
            Attributes = attributes
        });

        var response = await client.PostAsJsonAsync(url, attributes, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            Logger.LogInformation("<Handler> FormKiQ attributes set {StatusCode}", response.StatusCode);
        }
        else
        {
            Logger.LogError("<Handler> FormKiQ post failed {@Response}", response);
        }
    }

    private async Task SendSlackNotification(DocumentDetails documentDetails, List<string> labels, CancellationToken cancellationToken)
    {
        var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            Logger.LogWarning("<Handler> Slack webhook URL is not set");
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
            Logger.LogInformation("<Handler> Slack message sent {StatusCode}", response.StatusCode);
        }
        else
        {
            Logger.LogError("<Handler> Slack message failed {@Response}", response);
        }
    }
}
