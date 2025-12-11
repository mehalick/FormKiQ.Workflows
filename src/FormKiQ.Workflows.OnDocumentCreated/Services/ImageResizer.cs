using Amazon.S3;
using Amazon.S3.Model;
using FormKiQ.Workflows.OnDocumentCreated.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace FormKiQ.Workflows.OnDocumentCreated.Services;

public class ImageResizer
{
    private readonly ILogger<ImageResizer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAmazonS3 _s3Client;

    private const int LgImageWidth = 1024;
    private const int SmImageWidth = 256;

    public ImageResizer(ILogger<ImageResizer> logger, IConfiguration configuration, IAmazonS3 s3Client)
    {
        _logger = logger;
        _configuration = configuration;
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

            var lgImageBucket = document.S3Bucket;
            var lgImageKey = $"{Path.GetFileNameWithoutExtension(document.S3Key)}.png";

            var smImageBucket = _configuration["THUMBNAIL_BUCKET"];
            var smImageKey = $"{Path.GetFileNameWithoutExtension(document.S3Key)}.webp";

            if (string.IsNullOrWhiteSpace(smImageBucket))
            {
                throw new("Thumbnail bucket name environment variable 'THUMBNAIL_BUCKET' not set");
            }

            // Download the TIFF image from S3
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = document.S3Bucket,
                Key = document.S3Key
            };

            using var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest, cancellationToken);
            await using var responseStream = getObjectResponse.ResponseStream;

            // Load the image using ImageSharp
            using var image = await Image.LoadAsync(responseStream, cancellationToken);

            using (var resizedImage = image.Clone(ctx =>
                   {
                       ctx.Resize(new ResizeOptions
                       {
                           Size = new(LgImageWidth, 0),
                           Mode = ResizeMode.Max
                       });
                   }))
            {
                await using var ms = new MemoryStream();
                await resizedImage.SaveAsPngAsync(ms, cancellationToken);
                ms.Position = 0;

                await SaveImageToS3(lgImageBucket, lgImageKey, ms, cancellationToken);
            }

            using (var resizedImage = image.Clone(ctx =>
                   {
                       ctx.Resize(new ResizeOptions
                       {
                           Size = new(SmImageWidth),
                           Mode = ResizeMode.Pad,
                           Position = AnchorPositionMode.Center,
                           PadColor = Color.Black
                       });
                   }))
            {
                await using var ms = new MemoryStream();
                await resizedImage.SaveAsWebpAsync(ms, cancellationToken);
                ms.Position = 0;

                await SaveImageToS3(smImageBucket!, smImageKey, ms, cancellationToken);
            }

            return ResizeImageResult.Success(lgImageKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing image: {ErrorMessage}", ex.Message);
            return ResizeImageResult.Failure();
        }
    }

    private async Task SaveImageToS3(string bucket, string key, MemoryStream stream, CancellationToken cancellationToken)
    {
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            ContentType = "image/png"
        };

        var putObjectResponse = await _s3Client.PutObjectAsync(putObjectRequest, cancellationToken);

        _logger.LogInformation("Image converted and uploaded successfully. StatusCode: {StatusCode}, Key: {Key}",
            putObjectResponse.HttpStatusCode, key);
    }
}
