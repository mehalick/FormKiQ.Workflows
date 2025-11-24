using System.Net.Http.Json;
using Amazon.Textract;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public class TextProcessor
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TextProcessor> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonTextract _textractClient;

    public TextProcessor(IConfiguration configuration, ILogger<TextProcessor> logger, IHttpClientFactory httpClientFactory, IAmazonTextract textractClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _textractClient = textractClient;
    }

    public async Task SaveText(DocumentDetails document, string key, CancellationToken cancellationToken)
    {
        var words = await GetDocumentText(document, key, cancellationToken);
        await SetDocumentText(document, words, cancellationToken);
    }
    
    private async Task<List<string>> GetDocumentText(DocumentDetails document, string key, CancellationToken cancellationToken)
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
                        Name = key
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("Textract complete {StatusCode}", response.HttpStatusCode);

            var words = response.Blocks
                .Where(b => b.BlockType == BlockType.WORD)
                .Select(w => w.Text)
                .ToList();

            _logger.LogInformation("Textract complete {StatusCode}, {Count} words found", response.HttpStatusCode, words.Count);
            _logger.LogDebug("Textract: {Words}", string.Join(" ", words));

            return words;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error using Textract: {ErrorMessage}", ex.Message);
            return [];
        }
    }
    
    private async Task SetDocumentText(DocumentDetails document, List<string> words, CancellationToken cancellationToken)
    {
        if (words.Count == 0)
        {
            return;
        }

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

        var url = $"documents/{document.DocumentId}/ocr";
        var attributes = new
        {
            content = string.Join(' ', words),
            contentType = "text/plain",
            isBase64 = false
        };

        _logger.LogInformation("Sending attributes {@Words}", new
        {
            Url = url,
            ApiKey = formKiqApiKey,
            Attributes = attributes
        });

        var response = await client.PutAsJsonAsync(url, attributes, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("FormKiQ words set {StatusCode}", response.StatusCode);
        }
        else
        {
            _logger.LogError("FormKiQ post failed {@Response}", response);
        }
    }
}
