using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record LabelAttributeList(
    [property: JsonPropertyName("attributes")]
    List<LabelAttribute> Attributes)
{
    public static LabelAttributeList Create(List<string> labels, string thumbnail)
    {
        var attributes = new List<LabelAttribute>
        {
            new LabelAttribute("labels", labels),
            new LabelAttribute("thumbnail", [thumbnail])
        };

        return new(attributes);
    }
}
