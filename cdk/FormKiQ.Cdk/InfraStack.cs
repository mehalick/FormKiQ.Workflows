using Amazon.CDK;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SNS;
using Amazon.CDK.AWS.SNS.Subscriptions;
using Amazon.CDK.AWS.SQS;
using Constructs;
using FormKiQ.Cdk.Stacks;

namespace FormKiQ.Cdk;

public sealed class InfraStack : Stack
{
    internal InfraStack(Construct scope, string stackId, InfraStackProps props) : base(scope, stackId, props)
    {
        var topic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", props.SnsTopicArn);
        var queue = CreateQueueWithSnsSubscription("DocumentCreatedQueue", topic);

        var thumbnailBucket = this.CreateBucketDistribution(new()
        {
            Name = "Thumbnails",
            Description = "FormKiQ Thumbnails CDN",
            BucketName = $"formkiq-core-prod-thumbnails-{Account}",
            Versioned = true,
            HttpsMethods = [HttpMethods.GET, HttpMethods.HEAD],
            AllowsMethods = AllowedMethods.ALLOW_GET_HEAD
        });

        this.CreateBucketDistribution(new()
        {
            Name = "WebApp",
            Description = "FormKiQ Static Web App",
            BucketName = $"formkiq-core-prod-webapp-{Account}",
            Versioned = false,
            HttpsMethods = [HttpMethods.HEAD, HttpMethods.GET, HttpMethods.POST],
            AllowsMethods = AllowedMethods.ALLOW_ALL
        });

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
}
