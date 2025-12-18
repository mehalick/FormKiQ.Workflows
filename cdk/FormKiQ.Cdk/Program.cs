using Amazon.CDK;
using Microsoft.Extensions.Configuration;

namespace FormKiQ.Cdk;

internal abstract class Program
{
    public static void Main()
    {
        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets<Program>();

        var configuration = builder.Build();

        var app = new App();

        var props = new InfraStackProps("formkiq-workflows")
        {
            FormKiqBaseUrl = configuration["FormKiQ:BaseUrl"] ?? throw new("FormKiQ:BaseUrl is missing"),
            FormKiQApiKey = configuration["FormKiQ:ApiKey"] ??  throw new("FormKiQ:ApiKey is missing"),
            SlackWebhookUrl = configuration["Slack:WebhookUrl"] ??  throw new("Slack:WebhookUrl is missing"),
            SnsTopicArn = configuration["AWS:SnsTopicArn"] ??  throw new("AWS:SnsTopicArn is missing"),
            S3BucketName = configuration["AWS:S3BucketName"] ??  throw new("AWS:S3BucketName is missing")
        };

        _ = new InfraStack(app, props.StackId, props);

        app.Synth();
    }
}
