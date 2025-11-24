# Project Structure

## Solution Organization

The solution follows a clean separation between application code and infrastructure:

```
FormKiQ.Workflows/
├── apps/                           # Lambda function applications
│   └── FormKiQ.Workflows.OnDocumentCreated/
│       ├── Function.cs             # Lambda entry point with batch processing
│       ├── Handler.cs              # Record handler for individual messages
│       ├── Processor.cs            # Batch processor implementation
│       ├── Serializer.cs           # Custom serialization logic
│       ├── DocumentMessage.cs      # Message models
│       ├── DocumentDetails.cs      # Domain models
│       └── Dockerfile              # Lambda container image definition
├── infra/                          # Infrastructure as Code (CDK)
│   ├── FormKiQ.Workflows.Cdk/
│   │   ├── Program.cs              # CDK app entry point
│   │   └── InfraStack.cs           # Stack definition
│   ├── cdk.json                    # CDK configuration
│   └── deploy.ps1                  # Deployment script
├── docs/                           # Documentation
├── Directory.Build.props           # Shared MSBuild properties
└── Directory.Packages.props        # Central package version management
```

## Naming Conventions

### Projects
- Lambda functions: `FormKiQ.Workflows.<EventName>` (e.g., OnDocumentCreated)
- Infrastructure: `FormKiQ.Workflows.Cdk`

### Lambda Function Structure
Each Lambda function follows a consistent pattern:
- `Function.cs` - Entry point with instance-based `FunctionHandler` method, uses dependency injection
- `Startup.cs` - Configures dependency injection container (services, HttpClient, etc.)
- `Handler.cs` - Implements record-level processing logic with constructor injection
- `Processor.cs` - Batch processor for SQS messages
- `Serializer.cs` - Custom JSON serialization
- Domain models (e.g., `DocumentMessage.cs`, `DocumentDetails.cs`)
- `Dockerfile` - Multi-stage container build for Lambda deployment

## Configuration Files

### Solution Level
- `Directory.Build.props` - Shared build properties (target framework, language features)
- `Directory.Packages.props` - Central package version management
- `FormKiQ.Workflows.sln` - Solution file

### Infrastructure
- `infra/cdk.json` - CDK toolkit configuration
- `infra/deploy.ps1` - Deployment automation script

### Lambda Functions
- `aws-lambda-tools-defaults.json` - Lambda deployment defaults
- `Dockerfile` - Container image build instructions
- `.csproj` - Project file with Lambda-specific settings

## Architecture Patterns

### Lambda Functions
- Use AWS Lambda Powertools for logging and batch processing
- Implement batch processing pattern for SQS messages
- Dependency injection with Microsoft.Extensions.DependencyInjection
- ServiceProvider initialized in static constructor (once per cold start)
- Constructor injection for handlers and services
- IHttpClientFactory for HTTP client management
- Instance-based function handlers (not static)
- Attribute-based configuration (`[Logging]`)
- Container image deployment via Docker

### Infrastructure
- AWS CDK with C# for infrastructure definition
- Infrastructure code lives in `infra/` directory
- Separate from application code for clear boundaries

## Adding New Lambda Functions

When creating new Lambda functions:
1. Create new project under `apps/FormKiQ.Workflows.<EventName>/`
2. Follow the established pattern:
   - `Function.cs` - Instance-based handler with DI
   - `Startup.cs` - Configure services (AddHttpClient, register handlers)
   - `Handler.cs` - Constructor injection for dependencies
   - `Processor.cs` - Batch processor
   - Domain models and serializers
3. Include Dockerfile for container-based deployment
   - Use multi-stage build (SDK for build, Lambda base image for runtime)
   - Set CMD to the Lambda handler
   - Target linux-x64 runtime with PublishReadyToRun
4. Add package references: Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Http
5. Add project reference to solution file
6. Update CDK stack in `infra/FormKiQ.Workflows.Cdk/InfraStack.cs`
   - Use `Code.FromAssetImage()` for container deployment
   - Point to Dockerfile directory
   - Specify handler in `AssetImageCodeProps.Cmd`
