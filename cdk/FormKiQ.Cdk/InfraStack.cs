using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;
using Distribution = Amazon.CDK.AWS.CloudFront.Distribution;
using Function = Amazon.CDK.AWS.Lambda.Function;
using FunctionProps = Amazon.CDK.AWS.Lambda.FunctionProps;

namespace FormKiQ.Cdk;

public sealed class InfraStack : Stack
{
    private readonly string _stackId;
    private readonly string _formKiqBaseUrl;
    private readonly string _formKiQApiKey;
    private readonly string _slackWebhookUrl;

    internal InfraStack(Construct scope, string stackId, IConfiguration configuration, IStackProps props) : base(scope, stackId, props)
    {
        _stackId = stackId;
        _formKiqBaseUrl = configuration["FormKiQ:BaseUrl"] ??  throw new("BaseUrl is missing");
        _formKiQApiKey = configuration["FormKiQ:ApiKey"] ??  throw new("ApiKey is missing");
        _slackWebhookUrl = configuration["Slack:WebhookUrl"] ??  throw new("WebhookUrl is missing");

        var snsTopicArn = configuration["AWS:SnsTopicArn"] ??  throw new("SnsTopicArn is missing");
        var s3BucketName = configuration["AWS:S3BucketName"] ??  throw new("S3BucketName is missing");

        var topic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", snsTopicArn);
        var queue = CreateQueueWithSnsSubscription("DocumentCreatedQueue", topic);

        var thumbnailBucket = CreateThumbnailBucket();
        var policy = CreateSecurityHeadersPolicy(thumbnailBucket);
        _ = CreateCloudFrontDistribution(thumbnailBucket, policy);

        var function = CreateOnDocumentCreatedFunction(thumbnailBucket.BucketName);
        function.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps()));

        // Grant XRay permissions
        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "xray:PutTelemetryRecords",
                "xray:PutTraceSegments"
            ],
            Resources = ["*"]
        }));

        // Grant Rekognition permissions
        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "rekognition:DetectLabels",
                "rekognition:DetectFaces",
                "rekognition:DetectText"
            ],
            Resources = ["*"]
        }));

        // Grant Textract permissions
        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "textract:DetectDocumentText",
                "textract:AnalyzeDocument"
            ],
            Resources = ["*"]
        }));

        // Grant S3 read permissions for Rekognition to access images
        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "s3:GetObject",
                "s3:PutObject"
            ],
            Resources = [$"arn:aws:s3:::{s3BucketName}/*"]
        }));

        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "s3:PutObject"
            ],
            Resources = [$"{thumbnailBucket.BucketArn}/*"]
        }));
    }

    private Queue CreateQueueWithSnsSubscription(string queueName, ITopic snsTopic)
    {
        var dlq = new Queue(this, $"{queueName}DLQ");

        var queue = new Queue(this, queueName, new QueueProps
        {
            DeadLetterQueue = new DeadLetterQueue
            {
                Queue = dlq,
                MaxReceiveCount = 3
            }
        });

        snsTopic.AddSubscription(new SqsSubscription(queue));

        return queue;
    }

    private Bucket CreateThumbnailBucket()
    {
        var bucket = new Bucket(this, "ThumbnailBucket", new BucketProps
        {
            BucketName = $"formkiq-core-prod-thumbnails-{Account}",
            Versioned = true,

            // Configure server-side encryption (S3 managed by default)
            Encryption = BucketEncryption.S3_MANAGED,

            // Block all public access for security
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,

            // Configure lifecycle rules for cost optimization
            LifecycleRules =
            [
                new LifecycleRule
                {
                    Id = "DeleteIncompleteMultipartUploads",
                    Enabled = true,
                    AbortIncompleteMultipartUploadAfter = Duration.Days(7)
                },
                // new LifecycleRule
                // {
                //     Id = "TransitionToIA",
                //     Enabled = true,
                //     Transitions =
                //     [
                //         new Transition
                //         {
                //             StorageClass = StorageClass.INFREQUENT_ACCESS,
                //             TransitionAfter = Duration.Days(30)
                //         }
                //     ]
                // }
            ],

            // Enable event notifications (can be used for monitoring)
            //EventBridgeEnabled = true,

            // Configure CORS for web access (will be restricted to CloudFront)
            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = [HttpMethods.GET, HttpMethods.HEAD],
                    AllowedOrigins = ["*"], // Will be restricted by bucket policy
                    AllowedHeaders = ["*"],
                    MaxAge = 3000
                }
            ],

            // Remove bucket when stack is deleted (for development environments)
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        // Add bucket policy for CloudFront Origin Access Control
        // Note: The actual OAC will be created in a later task, this prepares the bucket
        AddBucketPolicyForCloudFront(bucket);

        return bucket;
    }

    /// <summary>
    /// Adds bucket policy to allow CloudFront Origin Access Control
    /// </summary>
    /// <param name="bucket">The S3 bucket to configure</param>
    private void AddBucketPolicyForCloudFront(Bucket bucket)
    {
        // The bucket policy will be updated after CloudFront distribution is created
        // This is a placeholder that will be updated in the UpdateBucketPolicyForDistribution method
    }

    /// <summary>
    /// Updates the bucket policy to allow access from the specific CloudFront distribution
    /// </summary>
    /// <param name="distribution">The CloudFront distribution</param>
    /// <param name="bucket"></param>
    private void UpdateBucketPolicyForDistribution(Distribution distribution, Bucket bucket)
    {
        // Create a policy statement that allows CloudFront OAC access from this specific distribution
        var allowOacStatement = new PolicyStatement(new PolicyStatementProps
        {
            Sid = "AllowCloudFrontServicePrincipal",
            Effect = Effect.ALLOW,
            Principals = [new ServicePrincipal("cloudfront.amazonaws.com")],
            Actions = ["s3:GetObject"],
            Resources = [bucket.ArnForObjects("*")],
            Conditions = new Dictionary<string, object>
            {
                ["StringEquals"] = new Dictionary<string, object>
                {
                    ["AWS:SourceArn"] = $"arn:aws:cloudfront::{Account}:distribution/{distribution.DistributionId}"
                }
            }
        });

        // Add the policy statement to the bucket
        bucket.AddToResourcePolicy(allowOacStatement);
    }

    /// <summary>
    /// Creates a CloudFront distribution with S3 as origin and configured caching behaviors
    /// </summary>
    /// <returns>The configured CloudFront distribution</returns>
    private Distribution CreateCloudFrontDistribution(Bucket bucket, ResponseHeadersPolicy responseHeadersPolicy)
    {
        // Create the CloudFront distribution
        var distribution = new Distribution(this, "CloudFrontDistribution", new DistributionProps
        {
            //Comment = _stackProps.DistributionComment,

            // Configure default cache behavior for web content
            DefaultBehavior = new BehaviorOptions
            {
                Origin = new S3Origin(bucket),
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED, // Good for static web content
                OriginRequestPolicy = OriginRequestPolicy.CORS_S3_ORIGIN, // Handle CORS for S3
                ResponseHeadersPolicy = responseHeadersPolicy, // Apply security headers if enabled
                Compress = true, // Enable compression for better performance
                AllowedMethods = AllowedMethods.ALLOW_GET_HEAD, // Only allow GET and HEAD for static content
                CachedMethods = CachedMethods.CACHE_GET_HEAD // Cache GET and HEAD responses
            },
            PriceClass = PriceClass.PRICE_CLASS_100,

            // No geographic restrictions by default

            // Enable IPv6 support
            EnableIpv6 = true,

            // Configure default root object
            DefaultRootObject = "index.html",

            // // Configure error responses for SPA applications
            // ErrorResponses =
            // [
            //     new ErrorResponse
            //     {
            //         HttpStatus = 404,
            //         ResponseHttpStatus = 200,
            //         ResponsePagePath = "/index.html", // Redirect 404s to index.html for SPA routing
            //         Ttl = Duration.Minutes(5)
            //     },
            //     new ErrorResponse
            //     {
            //         HttpStatus = 403,
            //         ResponseHttpStatus = 200,
            //         ResponsePagePath = "/index.html", // Redirect 403s to index.html for SPA routing
            //         Ttl = Duration.Minutes(5)
            //     }
            // ],

            // // Configure custom domain if specified
            // DomainNames = !string.IsNullOrEmpty(_stackProps.CustomDomainName)
            //     ? new[] { _stackProps.CustomDomainName }
            //     : null,
            //
            // Certificate = !string.IsNullOrEmpty(_stackProps.CertificateArn)
            //     ? Amazon.CDK.AWS.CertificateManager.Certificate.FromCertificateArn(this, "Certificate", _stackProps.CertificateArn)
            //     : null,

            // Configure access logging if enabled
            // EnableLogging = _stackProps.EnableAccessLogging,
            // LogBucket = AccessLogBucket,
            // LogFilePrefix = _stackProps.EnableAccessLogging ? _stackProps.AccessLogPrefix : null,

            // Configure minimum TLS version for security
            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,

            // Configure HTTP version
            HttpVersion = HttpVersion.HTTP2
        });


        return distribution;
    }

    /// <summary>
    /// Creates a response headers policy with security headers
    /// </summary>
    /// <returns>The configured response headers policy</returns>
    private ResponseHeadersPolicy CreateSecurityHeadersPolicy(Bucket bucket)
    {
        var securityHeadersPolicy = new ResponseHeadersPolicy(this, "SecurityHeadersPolicy", new ResponseHeadersPolicyProps
        {
            ResponseHeadersPolicyName = $"{bucket.BucketName}-security-headers",
            Comment = "Security headers policy for CloudFront distribution",

            // Configure security headers
            SecurityHeadersBehavior = new ResponseSecurityHeadersBehavior
            {
                // Strict Transport Security (HSTS)
                StrictTransportSecurity = new ResponseHeadersStrictTransportSecurity
                {
                    AccessControlMaxAge = Duration.Seconds(31536000), // 1 year
                    IncludeSubdomains = true,
                    Override = true
                },

                // Content Type Options
                ContentTypeOptions = new ResponseHeadersContentTypeOptions
                {
                    Override = true
                },

                // Frame Options
                FrameOptions = new ResponseHeadersFrameOptions
                {
                    FrameOption = HeadersFrameOption.DENY,
                    Override = true
                },

                // Referrer Policy
                ReferrerPolicy = new ResponseHeadersReferrerPolicy
                {
                    ReferrerPolicy = HeadersReferrerPolicy.STRICT_ORIGIN_WHEN_CROSS_ORIGIN,
                    Override = true
                }
            },

            // // Configure custom headers for additional security
            // CustomHeadersBehavior = new ResponseCustomHeadersBehavior
            // {
            //     CustomHeaders =
            //     [
            //         new ResponseCustomHeader
            //         {
            //             Header = "X-Content-Type-Options",
            //             Value = "nosniff",
            //             Override = true
            //         },
            //         new ResponseCustomHeader
            //         {
            //             Header = "X-XSS-Protection",
            //             Value = "1; mode=block",
            //             Override = true
            //         },
            //         new ResponseCustomHeader
            //         {
            //             Header = "Permissions-Policy",
            //             Value = "camera=(), microphone=(), geolocation=()",
            //             Override = true
            //         }
            //     ]
            // }
        });

        return securityHeadersPolicy;
    }

    private Function CreateOnDocumentCreatedFunction(string thumbnailBucket)
    {
        const string project = "FormKiQ.Workflows.OnDocumentCreated";
        const string dockerfilePath = $"../src/{project}";
        const string handler = $"{project}::{project}.Function::FunctionHandler";

        return new(this, "OnDocumentCreated",
            new FunctionProps
            {
                Code = Code.FromAssetImage(dockerfilePath, new AssetImageCodeProps
                {
                    Cmd = [handler]
                }),
                Description = "FormKiQ workflow to run on document created.",
                Handler = Handler.FROM_IMAGE,
                Runtime = Runtime.FROM_IMAGE,
                MemorySize = 2048,
                Architecture = Architecture.X86_64,
                Timeout = Duration.Seconds(90),
                LoggingFormat = LoggingFormat.JSON,
                LogGroup = new LogGroup(this, "OnDocumentCreatedLogGroup", new LogGroupProps
                {
                    LogGroupName = $"{_stackId}-OnDocumentCreatedLogGroup",
                    RemovalPolicy = RemovalPolicy.DESTROY,
                    Retention = RetentionDays.ONE_YEAR
                }),
                Tracing = Tracing.ACTIVE,
                Environment = new Dictionary<string, string>
                {
                    { "FORMKIQ_BASE_URL", _formKiqBaseUrl },
                    { "FORMKIQ_API_KEY", _formKiQApiKey },
                    { "SLACK_WEBHOOK_URL", _slackWebhookUrl },
                    { "THUMBNAIL_BUCKET", thumbnailBucket },
                    { "POWERTOOLS_SERVICE_NAME", "OnDocumentCreated" },
                    { "POWERTOOLS_LOG_LEVEL", "Information" },
                    { "POWERTOOLS_LOGGER_CASE", "CamelCase" }
                }
            });
    }
}
