using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;

namespace FormKiQ.Cdk.Stacks;

internal static class ComputeStack
{
    public static Function CreateOnDocumentCreatedFunction(this InfraStack stack, InfraStackProps props, string thumbnailBucket)
    {
        const string project = "FormKiQ.Workflows.OnDocumentCreated";
        const string dockerfilePath = $"../src/{project}";
        const string handler = $"{project}::{project}.Function::FunctionHandler";

        return new(stack, "OnDocumentCreated",
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
                Timeout = Duration.Seconds(30),
                LoggingFormat = LoggingFormat.JSON,
                LogGroup = new LogGroup(stack, "OnDocumentCreatedLogGroup", new LogGroupProps
                {
                    LogGroupName = $"{props.StackId}-OnDocumentCreatedLogGroup",
                    RemovalPolicy = RemovalPolicy.DESTROY,
                    Retention = RetentionDays.ONE_YEAR
                }),
                Tracing = Tracing.ACTIVE,
                Environment = new Dictionary<string, string>
                {
                    { "FORMKIQ_BASE_URL", props.FormKiqBaseUrl },
                    { "FORMKIQ_API_KEY", props.FormKiQApiKey },
                    { "SLACK_WEBHOOK_URL", props.SlackWebhookUrl },
                    { "THUMBNAIL_BUCKET", thumbnailBucket },
                    { "POWERTOOLS_SERVICE_NAME", "OnDocumentCreated" },
                    { "POWERTOOLS_LOG_LEVEL", "Information" },
                    { "POWERTOOLS_LOGGER_CASE", "CamelCase" }
                }
            });
    }

    public static void AddPermissions(this Function function, InfraStackProps props, Bucket thumbnailBucket)
    {
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

        // Grant read/write on FormKiQ documents bucket
        function.AddToRolePolicy(new(new PolicyStatementProps
        {
            Effect = Effect.ALLOW,
            Actions =
            [
                "s3:GetObject",
                "s3:PutObject"
            ],
            Resources = [$"arn:aws:s3:::{props.S3BucketName}/*"]
        }));

        //Grant write on thumbnails bucket
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
}
