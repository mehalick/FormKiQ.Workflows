using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormKiQ.App;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Document>> GetDocuments(string apiKey)
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", apiKey);

        const string url = "documents";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<Response>(url);

            if (response is null)
            {
                throw new($"{url} not found");
            }

            var document = response.Documents.First();

            var rand = new Random();

            for (var i = 0; i < 100; i++)
            {
                var d = new Document(
                    document.DocumentId,
                    document.UserId,
                    document.Path,
                    rand.NextInt64(0L, 100000L),
                    document.InsertedDate.AddDays(-i));

                response.Documents.Add(d);
            }

            return response.Documents;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public record Response(List<Document> Documents);

public record Document(
    string DocumentId,
    string UserId,
    string Path,
    long ContentLength,
    [property:JsonConverter(typeof(DateTimeOffsetConverterUsingDateTimeParse))]DateTimeOffset InsertedDate)
{
    public string ThumbnailUrl => $"https://dst7mdynk1sft.cloudfront.net/{DocumentId}.webp";
}

public class DateTimeOffsetConverterUsingDateTimeParse : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Debug.Assert(typeToConvert == typeof(DateTimeOffset));
        var input = reader.GetString();

        return string.IsNullOrWhiteSpace(input) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(input);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
