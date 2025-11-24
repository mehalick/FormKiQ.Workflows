using Amazon.S3;
using Amazon.S3.Model;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public class ImageResizer
{
    private readonly ILogger<ImageResizer> _logger;
    private readonly IAmazonS3 _s3Client;

    private const int MaxImagePixels = 1024;

    public ImageResizer(ILogger<ImageResizer> logger, IAmazonS3 s3Client)
    {
        _logger = logger;
        _s3Client = s3Client;
    }

    public record ResizeImageResult(string S3Key, bool IsSuccess)
    {
        public static ResizeImageResult Success(string s3Key)
        {
            return new(s3Key, true);
        }

        public static ResizeImageResult Failure()
        {
            return new("", false);
        }
    }

    public async Task<ResizeImageResult> ResizeImage(DocumentDetails document, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting image resize for document {DocumentId}", document.DocumentId);

            // Download the TIF image from S3
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = document.S3Bucket,
                Key = document.S3Key
            };

            using var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest, cancellationToken);
            await using var responseStream = getObjectResponse.ResponseStream;

            // Load the image using ImageSharp
            using var image = await Image.LoadAsync(responseStream, cancellationToken);

            // Convert to PNG and save to memory stream
            await using var pngStream = new MemoryStream();

            image.Mutate(i => i.Resize(new ResizeOptions
            {
                Size = new(MaxImagePixels, MaxImagePixels),
                Mode = ResizeMode.Max
            }));

            await image.SaveAsync(pngStream, new PngEncoder(), cancellationToken);

            pngStream.Position = 0;

            // Generate new key with .png extension
            var newKey = $"{Path.GetFileNameWithoutExtension(document.S3Key)}-thumbnail.png";

            // Upload the PNG image to the resize bucket
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = document.S3Bucket,
                Key = newKey,
                InputStream = pngStream,
                ContentType = "image/png"
            };

            var putObjectResponse = await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);

            _logger.LogInformation("Image converted and uploaded successfully. StatusCode: {StatusCode}, Key: {Key}",
                putObjectResponse.HttpStatusCode, newKey);

            return ResizeImageResult.Success(newKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing image: {ErrorMessage}", ex.Message);
            return ResizeImageResult.Failure();
        }
    }
}
