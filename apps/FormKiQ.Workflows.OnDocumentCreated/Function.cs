using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace FormKiQ.Workflows.OnDocumentCreated;

public class Function
{
    public OnDocumentCreatedResponse FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        var urls = sqsEvent.Records.Select(message => ProcessMessageAsync(message, context)).ToList();

        return new(string.Join(',', urls));
    }

    private static string ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        context.Logger.LogInformation($"HERE! Processed message {message.Body}");

        var request = JsonSerializer.Deserialize<MessageBody>(message.Body);

        if (request is null)
        {
            return "empty";
        }

        context.Logger.LogInformation("HERE! {@request}", request);

        var documentDetails = JsonSerializer.Deserialize<DocumentDetails>(request.Message);

        if (documentDetails is null)
        {
            return "empty";
        }

        context.Logger.LogInformation("HERE! {@document}", documentDetails);

        return documentDetails.Url;
    }
}

public record OnDocumentCreatedResponse(string Message);

public class MessageBody
{
    public string Type { get; init; } = string.Empty;

    public string MessageId { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public class DocumentDetails
{
    [JsonPropertyName("siteId")]
    public string SiteId { get; init; } = string.Empty;

    [JsonPropertyName("documentId")]
    public string DocumentId { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
