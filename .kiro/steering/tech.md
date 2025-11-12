# Technology Stack

## Framework & Runtime
- .NET 10.0 (net10.0)
- C# with latest language version
- Implicit usings enabled
- Nullable reference types enabled

## AWS Services & Libraries
- AWS Lambda (serverless compute)
- AWS CDK 2.220.0 (infrastructure as code)
- AWS Lambda Powertools 3.0.1 (logging, batch processing)
- Amazon SQS (message queuing)

## Key Dependencies
- Amazon.Lambda.Core 2.7.1
- Amazon.Lambda.RuntimeSupport 1.13.1
- Amazon.Lambda.SQSEvents 2.2.0
- Amazon.Lambda.Serialization.SystemTextJson 2.4.4
- AWS.Lambda.Powertools.BatchProcessing 3.0.1
- AWS.Lambda.Powertools.Logging 3.0.1
- Microsoft.Extensions.DependencyInjection 9.0.0
- Microsoft.Extensions.Http 9.0.0
- JetBrains.Annotations 2025.2.2

## Package Management
- Central Package Management enabled via Directory.Packages.props
- All package versions managed centrally at solution level

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
# Deploy infrastructure (from infra directory)
cd infra
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
