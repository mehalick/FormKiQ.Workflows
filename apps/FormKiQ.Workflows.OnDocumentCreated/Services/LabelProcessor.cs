using System.Net.Http.Json;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public class LabelProcessor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LabelProcessor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonRekognition _rekognitionClient;

    public LabelProcessor(IConfiguration configuration, ILogger<LabelProcessor> logger, IHttpClientFactory httpClientFactory, IAmazonRekognition rekognitionClient)
    {
        _configuration =  configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _rekognitionClient = rekognitionClient;
    }

    public async Task<List<string>> SaveLabels(DocumentDetails document, string key, CancellationToken cancellationToken)
    {
        var labels = await GetDocumentLabels(document, key, cancellationToken);
        await SetDocumentLabels(document, key, labels, cancellationToken);

        return labels;
    }

    private async Task<List<string>> GetDocumentLabels(DocumentDetails document, string key, CancellationToken cancellationToken)
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
                        Name = key
                    }
                },
                MaxLabels = 10,
                MinConfidence = 75F
            };

            var response = await _rekognitionClient.DetectLabelsAsync(request, cancellationToken);

            if (response.Labels.Count == 0)
            {
                _logger.LogInformation("No labels detected");
                return [];
            }

            foreach (var label in response.Labels)
            {
                _logger.LogDebug("Detecting label {@Label}", label);

                labels.Add(label.Name);
            }

            _logger.LogInformation("Labels detected {@Labels}", labels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error using Rekognition: {ErrorMessage}", ex.Message);
        }

        return labels;
    }

    private async Task SetDocumentLabels(DocumentDetails document, string key, List<string> labels, CancellationToken cancellationToken)
    {
        var formKiqBaseUrl = _configuration["FORMKIQ_BASE_URL"];
        var formKiqApiKey = _configuration["FORMKIQ_API_KEY"];

        if (string.IsNullOrEmpty(formKiqBaseUrl) || string.IsNullOrEmpty(formKiqApiKey))
        {
            _logger.LogWarning("FormKiQ base URL or API key not set");
            return;
        }

        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new(formKiqBaseUrl);
        client.DefaultRequestHeaders.Authorization = new(formKiqApiKey);

        var url = $"documents/{document.DocumentId}/attributes";
        var attributes = LabelAttributeList.Create(labels, key);

        _logger.LogInformation("Sending attributes {@Attributes}", new
        {
            Url = url,
            ApiKey = formKiqApiKey,
            Attributes = attributes
        });

        var response = await client.PostAsJsonAsync(url, attributes, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("FormKiQ attributes set {StatusCode}", response.StatusCode);
        }
        else
        {
            _logger.LogError("FormKiQ post failed {@Response}", response);
        }
    }
}
