namespace FormKiQ.Workflows.OnDocumentCreated.Models;

public record DocumentDetails(
    string SiteId,
    string DocumentId,
    string S3Key,
    string S3Bucket,
    string Type,
    string UserId,
    string Path,
    string Url);
