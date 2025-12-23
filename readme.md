# FormKiQ.Workflows

A serverless workflow automation system built on AWS Lambda for event-driven document processing in the FormKiQ document management platform.

## Documentation

For detailed information about the project, see the Kiro steering documents:

- [Product Overview](.kiro/steering/product.md) - What this project does
- [Technology Stack](.kiro/steering/tech.md) - Frameworks, libraries, and build commands
- [Project Structure](.kiro/steering/structure.md) - Organization, patterns, and conventions

## Deployment

To deploy the infrastructure and AWS Lambda code, you first need to set several .NET user secrets in the CDK Project:

```powershell
cd .\infra\FormKiQ.Workflows.Cdk\
dotnet user-secrets set "AWS:S3BucketName" "*********"
dotnet user-secrets set "AWS:SnsTopicArn" "*********"
dotnet user-secrets set "FormKiQ:ApiKey" "*********"
dotnet user-secrets set "FormKiQ:BaseUrl" "*********"
dotnet user-secrets set "Slack:WebhookUrl" "*********"
```

You can then deploy via:

```powershell
cdk deploy
```

## Web App

To run the web app locally, you'll need to set user secrets:

```powershell
cd .\src\FormKiQ.App
dotnet user-secrets init
dotnet user-secrets set "CloudFrontHost", "*********"
```

You can deploy manually via:

```asm
 aws s3 sync ".\src\FormKiQ.App\bin\Release\net10.0\publish\wwwroot" {S3BucketArn}
```
