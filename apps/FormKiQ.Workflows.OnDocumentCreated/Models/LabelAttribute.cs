using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record LabelAttribute([property: JsonPropertyName("stringValues")] List<string> StringValues)
{
    [JsonPropertyName("key")]
    public const string Key = "labels";
}