using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record LabelAttribute(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("stringValues")]
    List<string> StringValues);
