using Amazon.CDK;

namespace FormKiQ.Cdk;

internal class InfraStackProps(string stackId) : StackProps
{
    public string StackId { get; } = stackId;
    public required string FormKiqBaseUrl { get; init; }
    public required string FormKiQApiKey { get; init; }
    public required string SlackWebhookUrl { get; init; }
    public required string SnsTopicArn { get; init; }
    public required string S3BucketName { get; init; }
}
