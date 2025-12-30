using Amazon.CDK;
using Microsoft.Extensions.Configuration;

namespace FormKiQ.Cdk;

internal abstract class Program
{
    public static void Main()
    {
        var builder = new ConfigurationBuilder();
        builder.AddEnvironmentVariables();
        builder.AddUserSecrets<Program>();

        var configuration = builder.Build();

        var app = new App();

        var props = new InfraStackProps("formkiq-workflows")
        {
            FormKiqBaseUrl = configuration["FORMKIQ__BASEURL"] ?? throw new("FormKiQ:BaseUrl is missing"),
            FormKiQApiKey = configuration["FORMKIQ__APIKEY"] ??  throw new("FormKiQ:ApiKey is missing"),
            SlackWebhookUrl = configuration["SLACK__WEBHOOKURL"] ??  throw new("Slack:WebhookUrl is missing"),
            SnsTopicArn = configuration["AWS__SNSTOPICARN"] ??  throw new("AWS:SnsTopicArn is missing"),
            S3BucketName = configuration["AWS__S3BUCKETNAME"] ??  throw new("AWS:S3BucketName is missing")
        };

        _ = new InfraStack(app, props.StackId, props);

        app.Synth();
    }
}
