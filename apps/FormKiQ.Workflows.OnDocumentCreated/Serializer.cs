using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated;

[JsonSerializable(typeof(DocumentDetails))]
[JsonSerializable(typeof(DocumentMessage))]
[JsonSerializable(typeof(DocumentMessage[]))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public partial class Serializer : JsonSerializerContext;

