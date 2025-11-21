using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated.Services.Slack;

public class TextObject
{
    [JsonPropertyName("text")]
    public string Type { get; } = "mrkdwn";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    public TextObject()
    {

    }

    public TextObject(string text)
    {
        Text = text;
    }
}

public class Section
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "section";

    [JsonPropertyName("text")]
    public TextObject Text { get; set; } = new TextObject();
}

public class Message
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("blocks")]
    public List<Section> Blocks { get; set; } = [];
}
