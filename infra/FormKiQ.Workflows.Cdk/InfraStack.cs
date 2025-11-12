using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Microsoft.Extensions.Configuration;

namespace FormKiQ.Workflows.Cdk;

public class InfraStack : Stack
{
    internal InfraStack(Construct scope, string id, IConfiguration configuration, IStackProps props) : base(scope, id, props)
    {
        var snsTopicArn = configuration["AWS:SnsTopicArn"];
        
        if (string.IsNullOrWhiteSpace(snsTopicArn))
        {
            throw new("SNS Topic ARN is missing");
        }
        
        var topic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", snsTopicArn);
        var queue = CreateQueueWithSnsSubscription("DocumentCreatedQueue", topic);

        var function = CreateOnDocumentCreatedFunction(configuration);
        function.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps()));
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

    private Function CreateOnDocumentCreatedFunction(IConfiguration configuration)
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
                    { "FORMKIQ_API_KEY", formKiQApiKey }
                }
            });
    }
}
