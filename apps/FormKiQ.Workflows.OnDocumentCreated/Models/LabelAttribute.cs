using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record LabelAttribute([property: JsonPropertyName("stringValues")] List<string> StringValues)
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "labels";
}
