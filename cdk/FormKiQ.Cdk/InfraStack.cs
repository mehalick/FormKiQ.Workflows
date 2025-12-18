using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using FormKiQ.Cdk.Stacks;
using Distribution = Amazon.CDK.AWS.CloudFront.Distribution;

namespace FormKiQ.Cdk;

public sealed class InfraStack : Stack
{
    internal InfraStack(Construct scope, string stackId, InfraStackProps props) : base(scope, stackId, props)
    {
        var topic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", props.SnsTopicArn);
        var queue = CreateQueueWithSnsSubscription("DocumentCreatedQueue", topic);

        var thumbnailBucket = this.CreateBucketDistribution(props, DistributionStack.BucketType.Cdn);

        var webAppBucket = CreateWebAppBucket();
        _ = CreateWebAppDistribution(webAppBucket);

        var function = this.CreateOnDocumentCreatedFunction(props, thumbnailBucket.BucketName);
        function.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps()));
        function.AddPermissions(props, thumbnailBucket);
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

    private Bucket CreateWebAppBucket()
    {
        var bucket = new Bucket(this, "WebAppBucket", new BucketProps
        {
            BucketName = $"formkiq-core-prod-webapp-{Account}",
            Versioned = false,
            Encryption = BucketEncryption.S3_MANAGED,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,

            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = [HttpMethods.HEAD, HttpMethods.GET, HttpMethods.POST],
                    AllowedOrigins = ["*"],
                    AllowedHeaders = ["*"],
                    MaxAge = 3000
                }
            ],

            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true
        });

        return bucket;
    }

    private Distribution CreateWebAppDistribution(Bucket bucket)
    {
        var s3Origin = S3BucketOrigin.WithOriginAccessControl(bucket);

        var distribution = new Distribution(this, "WebAppDistribution", new DistributionProps
        {
            Comment = "FormKiQ Static Web App",

            DefaultBehavior = new BehaviorOptions
            {
                Origin = s3Origin,
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = CachePolicy.CACHING_DISABLED,
                OriginRequestPolicy = OriginRequestPolicy.CORS_S3_ORIGIN,
                //ResponseHeadersPolicy = policy,
                Compress = true,
                AllowedMethods = AllowedMethods.ALLOW_ALL,
                CachedMethods = CachedMethods.CACHE_GET_HEAD
            },
            PriceClass = PriceClass.PRICE_CLASS_100,

            // No geographic restrictions by default

            EnableIpv6 = true,
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

            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
            HttpVersion = HttpVersion.HTTP2
        });

        // _ = new BucketDeployment(this, "DeployWebsiteContent", new BucketDeploymentProps
        // {
        //     Sources = [Source.Asset("../src/FormKiQ.App/bin/Release/net10.0/publish/wwwroot")],
        //     DestinationBucket = bucket,
        //     Distribution = distribution,
        //     DistributionPaths = ["/*"] // Invalidate CloudFront cache after deployment
        // });

        return distribution;
    }
}
