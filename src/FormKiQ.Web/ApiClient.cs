using System.Net.Http.Json;

namespace FormKiQ.Web;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Document>> GetDocuments()
    {
        _httpClient.DefaultRequestHeaders.Add("Authorization", "C05H2KZZZ5HTVAaXsWFmSyBKDdQQ8rfKv01Dij7Do1jiOvEfwSa");

        const string url = "documents?limit=10&date=2025-12-11";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<Response>(url);

            foreach (var document in response.Documents)
            {
                document.ThumbnailUrl = $"https://dsrgeb4klvmsf.cloudfront.net/{document.DocumentId}.webp";
            }

            return response?.Documents ?? [];
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}

public class Response
{
    public List<Document> Documents { get; set; } = [];
}

public class Document
{
    public string DocumentId { get; set; } = "";

    public string ThumbnailUrl { get; set; } = "";
}
