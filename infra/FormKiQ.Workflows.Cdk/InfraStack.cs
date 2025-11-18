using Amazon.CDK;
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

namespace FormKiQ.Workflows.Cdk;

public sealed class InfraStack : Stack
{
    internal InfraStack(Construct scope, string id, IConfiguration configuration, IStackProps props) : base(scope, id, props)
    {
        var snsTopicArn = configuration["AWS:SnsTopicArn"];
        var s3BucketName = configuration["AWS:S3BucketName"];

        if (string.IsNullOrWhiteSpace(snsTopicArn) || string.IsNullOrWhiteSpace(s3BucketName))
        {
            throw new("SNS topic ARN or S3 bucket name are missing");
        }

        // Create S3 bucket for resized images
        var resizeBucket = new Bucket(this, "ResizeBucket", new BucketProps
        {
            BucketName = $"formkiq-workflows-resize-{Account}",
            RemovalPolicy = RemovalPolicy.RETAIN,
            Versioned = false,
            PublicReadAccess = false,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.S3_MANAGED,
            LifecycleRules =
            [
                new LifecycleRule
                {
                    Enabled = true,
                    Expiration = Duration.Days(30)
                }
            ]
        });

        var topic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", snsTopicArn);
        var queue = CreateQueueWithSnsSubscription("DocumentCreatedQueue", topic);

        var function = CreateOnDocumentCreatedFunction(configuration, resizeBucket.BucketName);
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
                "rekognition:DetectText",
                "rekognition:DetectModerationLabels",
                "rekognition:RecognizeCelebrities"
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
                "s3:GetObjectVersion"
            ],
            Resources = [$"arn:aws:s3:::{s3BucketName}/*"]
        }));

        // Grant S3 write permissions to resize bucket
        resizeBucket.GrantReadWrite(function);
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

    private Function CreateOnDocumentCreatedFunction(IConfiguration configuration, string resizeBucketName)
    {
        const string project = "FormKiQ.Workflows.OnDocumentCreated";
        const string dockerfilePath = $"../apps/{project}";
        const string handler = $"{project}::{project}.Function::FunctionHandler";

        var formKiqBaseUrl = configuration["FormKiQ:BaseUrl"];

        if (string.IsNullOrWhiteSpace(formKiqBaseUrl))
        {
            throw new("FormKiQ API URL is missing");
        }

        var formKiQApiKey = configuration["FormKiQ:ApiKey"];

        if (string.IsNullOrWhiteSpace(formKiQApiKey))
        {
            throw new("FormKiQ API Key is missing");
        }

        var slackWebhookUrl = configuration["Slack:WebhookUrl"];

        if (string.IsNullOrWhiteSpace(slackWebhookUrl))
        {
            throw new("Slack Webhook URL is missing");
        }

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
                MemorySize = 256,
                Architecture = Architecture.X86_64,
                Timeout = Duration.Seconds(30),
                LoggingFormat = LoggingFormat.JSON,
                LogGroup = new LogGroup(this, "OnDocumentCreatedLogGroup", new LogGroupProps
                {
                    RemovalPolicy = RemovalPolicy.DESTROY,
                    Retention = RetentionDays.ONE_YEAR
                }),
                Tracing = Tracing.ACTIVE,
                Environment = new Dictionary<string, string>
                {
                    { "SLACK_WEBHOOK_URL", slackWebhookUrl },
                    { "FORMKIQ_BASE_URL", formKiqBaseUrl },
                    { "FORMKIQ_API_KEY", formKiQApiKey },
                    { "RESIZE_BUCKET_NAME", resizeBucketName }
                }
            });
    }
}
