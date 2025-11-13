using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record LabelAttributeList(
    [property: JsonPropertyName("attributes")]
    List<LabelAttribute> Attributes)
{
    public static LabelAttributeList Create(List<string> labels)
    {
        return new([new(labels)]);
    }
}