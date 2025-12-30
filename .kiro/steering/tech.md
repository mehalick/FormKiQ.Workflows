# Technology Stack

## Framework & Runtime
- .NET 10.0 (net10.0)
- C# with latest language version
- Implicit usings enabled
- Nullable reference types enabled

## AWS Services & Libraries
- AWS Lambda (serverless compute)
- AWS CDK 2.225.0 (infrastructure as code)
- AWS Lambda Powertools 3.0.2 (logging, batch processing)
- Amazon SQS (message queuing)
- AWS Rekognition (image analysis)
- AWS Textract (document text extraction)
- Amazon S3 (object storage)

## Key Dependencies
- Amazon.Lambda.Core 2.8.0
- Amazon.Lambda.SQSEvents 2.2.0
- Amazon.Lambda.Serialization.SystemTextJson 2.4.4
- AWS.Lambda.Powertools.BatchProcessing 3.0.2
- AWS.Lambda.Powertools.Logging 3.0.2
- Microsoft.Extensions.DependencyInjection 10.0.0
- Microsoft.Extensions.Http 10.0.0
- Microsoft.Extensions.Configuration.EnvironmentVariables 10.0.0
- AWSSDK.Extensions.NETCore.Setup 4.0.3.12
- AWSSDK.Rekognition 4.0.3.3
- AWSSDK.S3 4.0.11.3
- AWSSDK.Textract 4.0.3.3
- SixLabors.ImageSharp 3.1.12
- JetBrains.Annotations 2025.2.2

## Package Management
- Individual package references in each .csproj file
- Package versions managed per project (no central package management currently)
- Consider implementing Central Package Management via Directory.Packages.props for better version consistency

## Build & Deployment

### Build Commands
```powershell
# Build entire solution
dotnet build FormKiQ.Workflows.sln

# Build in Release mode
dotnet build FormKiQ.Workflows.sln -c Release
```

### Infrastructure Deployment
```powershell
# Deploy infrastructure (from cdk directory)
cd cdk
.\deploy.ps1

# Or manually:
dotnet build ./../FormKiQ.Workflows.sln -c Release
cdk deploy
```

### CDK Commands
```powershell
# Synthesize CloudFormation template
cdk synth

# Show differences with deployed stack
cdk diff

# Deploy stack
cdk deploy
```

### Lambda Deployment
Lambda functions are packaged as Docker container images and deployed via CDK infrastructure.

The Dockerfile uses multi-stage builds:
- Build stage: Uses .NET 10 SDK to compile and publish the Lambda function
- Final stage: Uses AWS Lambda .NET 10 base image with published artifacts
- Runtime: linux-x64 with PublishReadyToRun enabled for faster cold starts

Container images are automatically built and pushed to Amazon ECR during CDK deployment.

## Development Tools
- Visual Studio 2017+ (solution format v12.00)
- AWS Lambda Tools (for local testing and deployment)
- Docker (for Lambda container images)
