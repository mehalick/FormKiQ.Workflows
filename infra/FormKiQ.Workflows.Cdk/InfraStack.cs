using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;

namespace FormKiQ.Workflows.Cdk;

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

        var function = CreateOnDocumentCreatedFunction();
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

    private Function CreateOnDocumentCreatedFunction()
    {
        const string project = "FormKiQ.Workflows.OnDocumentCreated";
        const string dockerfilePath = $"../apps/{project}";
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
                    { "POWERTOOLS_SERVICE_NAME", "OnDocumentCreated" },
                    { "POWERTOOLS_LOG_LEVEL", "Information" },
                    { "POWERTOOLS_LOGGER_CASE", "CamelCase" }
                }
            });
    }
}
