using System.Text.Json.Serialization;

namespace FormKiQ.Workflows.OnDocumentCreated;

public record DocumentDetails(
    string SiteId,
    string DocumentId,
    string S3Key,
    string S3Bucket,
    string Type,
    string UserId,
    string Path,
    string Url);
