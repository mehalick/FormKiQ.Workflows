using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.S3;

namespace FormKiQ.Cdk.Stacks;

internal static class DistributionStack
{
    public class Properties
    {
        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string BucketName { get; init; }
        public required bool Versioned { get; init; }

        public required HttpMethods[] HttpsMethods { get; init; }

        public required AllowedMethods AllowsMethods { get; init; }
    }

    public static Bucket CreateBucketDistribution(this InfraStack stack, Properties props)
    {
        var bucket = CreateThumbnailsBucket(stack, props);
        _ = CreateThumbnailsDistribution(stack, bucket, props);

        return bucket;
    }

    private static Bucket CreateThumbnailsBucket(InfraStack stack, Properties props)
    {
        var bucket = new Bucket(stack, $"{props.Name}Bucket", new BucketProps
        {
            BucketName = props.BucketName,
            Versioned = props.Versioned,
            Encryption = BucketEncryption.S3_MANAGED,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,

            // LifecycleRules =
            // [
            //     new LifecycleRule
            //     {
            //         Id = "DeleteIncompleteMultipartUploads",
            //         Enabled = true,
            //         AbortIncompleteMultipartUploadAfter = Duration.Days(7)
            //     }
            // ],

            Cors =
            [
                new CorsRule
                {
                    AllowedMethods = props.HttpsMethods,
                    AllowedOrigins = ["*"],
                    AllowedHeaders = ["*"],
                    MaxAge = 3000
                }
            ],

            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = false
        });

        return bucket;
    }

    private static Distribution CreateThumbnailsDistribution(InfraStack stack, Bucket bucket, Properties props)
    {
        var policy = CreateSecurityHeadersPolicy(stack, bucket, props);
        var s3Origin = S3BucketOrigin.WithOriginAccessControl(bucket);

        var distribution = new Distribution(stack, $"{props.Name}Distribution", new DistributionProps
        {
            Comment = props.Description,

            DefaultBehavior = new BehaviorOptions
            {
                Origin = s3Origin,
                ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,
                CachePolicy = CachePolicy.CACHING_OPTIMIZED,
                OriginRequestPolicy = OriginRequestPolicy.CORS_S3_ORIGIN,
                ResponseHeadersPolicy = policy,
                Compress = true,
                AllowedMethods = props.AllowsMethods,
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

            MinimumProtocolVersion = SecurityPolicyProtocol.TLS_V1_2_2021,
            HttpVersion = HttpVersion.HTTP2
        });

        return distribution;
    }

        /// <summary>
    /// Creates a response headers policy with security headers
    /// </summary>
    /// <returns>The configured response headers policy</returns>
    private static ResponseHeadersPolicy CreateSecurityHeadersPolicy(InfraStack stack, Bucket bucket, Properties props)
    {
        return new(stack, $"{props.Name}SecurityHeadersPolicy", new ResponseHeadersPolicyProps
        {
            ResponseHeadersPolicyName = $"{bucket.BucketName}-security-headers",
            Comment = $"Security headers policy for CloudFront {bucket.BucketName} distribution",

            SecurityHeadersBehavior = new ResponseSecurityHeadersBehavior
            {
                StrictTransportSecurity = new ResponseHeadersStrictTransportSecurity
                {
                    AccessControlMaxAge = Duration.Seconds(31536000), // 1 year
                    IncludeSubdomains = true,
                    Override = true
                },

                ContentTypeOptions = new ResponseHeadersContentTypeOptions
                {
                    Override = true
                },

                FrameOptions = new ResponseHeadersFrameOptions
                {
                    FrameOption = HeadersFrameOption.DENY,
                    Override = true
                },

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
    }
}
