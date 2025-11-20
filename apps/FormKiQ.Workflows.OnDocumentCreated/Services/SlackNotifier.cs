using System.Net.Http.Json;
using Amazon.S3;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public class SlackNotifier
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SlackNotifier> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonS3 _s3Client;

    public SlackNotifier(IConfiguration configuration, ILogger<SlackNotifier> logger, IHttpClientFactory httpClientFactory, IAmazonS3 s3Client)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _s3Client = s3Client;
    }

    public async Task SendNotification(DocumentDetails document, string key, List<string> labels, CancellationToken cancellationToken)
    {
        var presignedUrl = await GetPresignedUrl(document.S3Bucket, key);
        await SendSlackNotification(document, presignedUrl, labels, cancellationToken);
    }
    
    private async Task<string> GetPresignedUrl(string bucket, string key)
    {
        return await _s3Client.GetPreSignedURLAsync(new()
        {
            BucketName = bucket,
            Key = key,
            Expires = DateTime.UtcNow.AddDays(7),
            Verb = HttpVerb.GET
        });
    }
    
    private async Task SendSlackNotification(DocumentDetails documentDetails, string presignedUrl, List<string> labels, CancellationToken cancellationToken)
    {
        var slackWebhookUrl = _configuration["SLACK_WEBHOOK_URL"];

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            _logger.LogWarning("Slack webhook URL is not set");
            return;
        }

        var labelList = string.Join(", ", labels);

        var json = new
        {
            text = $"New document <{presignedUrl}|{documentDetails.Path}> uploaded, labels: {labelList}"
        };

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsJsonAsync(slackWebhookUrl, json, cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Slack message sent {StatusCode}", response.StatusCode);
        }
        else
        {
            _logger.LogError("Slack message failed {@Response}", response);
        }
    }
}
