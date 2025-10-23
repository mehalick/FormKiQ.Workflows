using Amazon.CDK;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
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

        var function = CreateActivityUpdatedFunction();

        function.AddEventSource(new SqsEventSource(queue, new SqsEventSourceProps
        {
            //BatchSize = 1, // Optional: Number of messages to process in a batch
            //MaxBatchingWindow = Amazon.CDK.Duration.Seconds(5) // Optional: Max time to gather messages
        }));

        // var dockerImageAsset = new DockerImageAsset(this, "MyLambdaDockerImage", new DockerImageAssetProps
        // {
        //     Directory = "../App3/src/App3"
        // });
        //
        // new DockerImageFunction(this, "MyLambdaFunction", new DockerImageFunctionProps
        // {
        //     Code = DockerImageCode.FromEcr(dockerImageAsset.Repository, new EcrImageCodeProps { TagOrDigest = dockerImageAsset.ImageTag }),
        //     MemorySize = 512,
        //     Timeout = Duration.Seconds(30),
        //     Architecture = Architecture.ARM_64
        // });
    }

    private Queue CreateQueueWithSnsSubscription(string queueName, ITopic snsTopic)
    {
        var queue = new Queue(this, queueName);
        snsTopic.AddSubscription(new SqsSubscription(queue));
        return queue;
    }

    private Function CreateActivityUpdatedFunction()
    {
        const string f = "../apps/FormKiQ.Workflows.OnDocumentCreated/bin/Release/net8.0";

        var activityUpdatedFunction = new Function(this, "OnDocumentCreated",
            new FunctionProps
            {
                //FunctionName = "fk-OnDocumentCreated",
                Code = Code.FromAsset(f),
                Description = "FormKiQ workflow to run on document created.",
                Handler = "FormKiQ.Workflows.OnDocumentCreated::FormKiQ.Workflows.OnDocumentCreated.Function::FunctionHandler",
                MemorySize = 256,
                Runtime = Runtime.DOTNET_8,
                Architecture = Architecture.ARM_64,
                Timeout = Duration.Seconds(30),
                LoggingFormat = LoggingFormat.JSON,

            });


        return activityUpdatedFunction;
    }
}
