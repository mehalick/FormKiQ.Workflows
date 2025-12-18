using System.Net.Http.Json;
using Amazon.S3;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using FormKiQ.Workflows.OnDocumentCreated.Services.Slack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public record SendNotificationRequest(DocumentDetails Document, string S3Key, List<string> Labels)
{
    public string LabelList => string.Join(", ", Labels);
}

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

    public async Task SendNotification(SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var presignedUrl = await GetPresignedUrl(request.Document.S3Bucket, request.S3Key);
        await SendSlackNotification(request, presignedUrl, cancellationToken);
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

    private async Task SendSlackNotification(SendNotificationRequest request, string presignedUrl, CancellationToken cancellationToken)
    {
        var slackWebhookUrl = _configuration["SLACK_WEBHOOK_URL"];

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            _logger.LogWarning("Slack webhook URL is not set");
            return;
        }

        var json = new
        {
            text = $"New document <{presignedUrl}|{request.Document.Path}> uploaded, labels: {request.LabelList}"
        };

        var message = new Message
        {
            Text = "New document uploaded",
            Blocks =
            [
                new Section
                {
                    Text = new($"Image: <{presignedUrl}|{request.Document.Path}>")
                },
                new Section
                {
                    Text = new($"Labels: {request.LabelList}")
                },
            ]
        };

        // TODO: need to POST with HttpContent or create strongly-typed object (maybe NuGet package?)
        var j = $$"""
                  {
                      "text": "Document Uploaded",
                      "blocks": [
                      	{
                      		"type": "section",
                      		"text": {
                      			"type": "mrkdwn",
                      			"text": "{{request.LabelList}}"
                      		}
                      	},
                      	{
                      		"type": "section",
                      		"block_id": "section567",
                      		"text": {
                      			"type": "mrkdwn",
                      			"text": "<https://example.com|Overlook Hotel> \n :star: \n Doors had too many axe holes, guest in room 237 was far too rowdy, whole place felt stuck in the 1920s."
                      		},
                      		"accessory": {
                      			"type": "image",
                      			"image_url": "{{presignedUrl}}",
                      			"alt_text": "{{request.Document.DocumentId}}"
                      		}
                      	},
                      	{
                      		"type": "section",
                      		"block_id": "section789",
                      		"fields": [
                      			{
                      				"type": "mrkdwn",
                      				"text": "*Average Rating*\n1.0"
                      			}
                      		]
                      	}
                      ]
                  }
                  """;

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
