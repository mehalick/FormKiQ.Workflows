using Amazon.CDK;
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
        var existingTopic = Topic.FromTopicArn(this, "SnsDocumentEventTopic", configuration["AWS:SnsTopicArn"]!);
        var queue = CreateQueueWithSnsSubscription("DocumentEventQueue", existingTopic);
    }

    private Queue CreateQueueWithSnsSubscription(string queueName, ITopic snsTopic)
    {
        var queue = new Queue(this, queueName);
        snsTopic.AddSubscription(new SqsSubscription(queue));
        return queue;
    }
}
