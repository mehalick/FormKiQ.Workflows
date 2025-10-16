using Amazon.CDK;
using Microsoft.Extensions.Configuration;

namespace FormKiQ.Workflows.Cdk;

internal abstract class Program
{
    public static void Main()
    {
        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets<Program>();

        var configuration = builder.Build();

        var app = new App();
        _ = new InfraStack(app, "formkiq-workflows", configuration, new StackProps());

        app.Synth();
    }
}
