# Project Structure

## Solution Organization

The solution follows a clean separation between application code and infrastructure:

```
FormKiQ.Workflows/
├── src/                            # Source code applications
│   ├── FormKiQ.Workflows.OnDocumentCreated/  # Lambda function for document processing
│   │   ├── Function.cs             # Lambda entry point with batch processing
│   │   ├── Handler.cs              # Record handler for individual messages
│   │   ├── Processor.cs            # Batch processor implementation
│   │   ├── Serializer.cs           # Custom serialization logic
│   │   ├── Startup.cs              # Dependency injection configuration
│   │   ├── Models/                 # Domain models (DocumentMessage, DocumentDetails, etc.)
│   │   ├── Services/               # Business logic services (ImageResizer, TextProcessor, etc.)
│   │   └── Dockerfile              # Lambda container image definition
│   ├── FormKiQ.App/                # Blazor WebAssembly application
│   ├── FormKiQ.Reporting/          # Reporting console application
│   └── FormKiQ.Uploader/           # File upload utility
├── cdk/                            # Infrastructure as Code (CDK)
│   ├── FormKiQ.Cdk/
│   │   ├── Program.cs              # CDK app entry point
│   │   ├── InfraStack.cs           # Main stack definition
│   │   ├── InfraStackProps.cs      # Stack properties
│   │   └── Stacks/                 # Additional stack definitions
│   ├── cdk.json                    # CDK configuration
│   └── deploy.ps1                  # Deployment script
├── apps/                           # Application hosting configurations
│   └── FormKiQ.Web/               # Web application hosting
├── docs/                           # Documentation
└── Directory.Build.props           # Shared MSBuild properties
```

## Naming Conventions

### Projects
- Lambda functions: `FormKiQ.Workflows.<EventName>` (e.g., OnDocumentCreated)
- Infrastructure: `FormKiQ.Cdk` (note: not FormKiQ.Workflows.Cdk)
- Applications: `FormKiQ.<AppName>` (e.g., FormKiQ.App, FormKiQ.Reporting)

### Lambda Function Structure
Each Lambda function follows a consistent pattern:
- `Function.cs` - Entry point with instance-based `FunctionHandler` method, uses dependency injection
- `Startup.cs` - Configures dependency injection container (services, HttpClient, etc.)
- `Handler.cs` - Implements record-level processing logic with constructor injection
- `Processor.cs` - Batch processor for SQS messages
- `Serializer.cs` - Custom JSON serialization
- `Models/` directory - Domain models (e.g., `DocumentMessage.cs`, `DocumentDetails.cs`)
- `Services/` directory - Business logic services (e.g., `ImageResizer.cs`, `TextProcessor.cs`)
- `Dockerfile` - Multi-stage container build for Lambda deployment

## Configuration Files

### Solution Level
- `Directory.Build.props` - Shared build properties (target framework, language features)
- `FormKiQ.Workflows.sln` - Solution file
- Note: Central Package Management is not currently enabled (no Directory.Packages.props)

### Infrastructure
- `cdk/cdk.json` - CDK toolkit configuration
- `cdk/deploy.ps1` - Deployment automation script

### Lambda Functions
- `aws-lambda-tools-defaults.json` - Lambda deployment defaults
- `Dockerfile` - Container image build instructions
- `.csproj` - Project file with Lambda-specific settings and individual package references

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
- Infrastructure code lives in `cdk/` directory (not `infra/`)
- Separate from application code for clear boundaries

## Adding New Lambda Functions

When creating new Lambda functions:
1. Create new project under `src/FormKiQ.Workflows.<EventName>/`
2. Follow the established pattern:
   - `Function.cs` - Instance-based handler with DI
   - `Startup.cs` - Configure services (AddHttpClient, register handlers)
   - `Handler.cs` - Constructor injection for dependencies
   - `Processor.cs` - Batch processor
   - `Models/` directory for domain models
   - `Services/` directory for business logic
   - Serializers as needed
3. Include Dockerfile for container-based deployment
   - Use multi-stage build (SDK for build, Lambda base image for runtime)
   - Set CMD to the Lambda handler
   - Target linux-x64 runtime with PublishReadyToRun
4. Add package references directly to .csproj (no central package management currently)
5. Add project reference to solution file
6. Update CDK stack in `cdk/FormKiQ.Cdk/InfraStack.cs`
   - Use `Code.FromAssetImage()` for container deployment
   - Point to Dockerfile directory
   - Specify handler in `AssetImageCodeProps.Cmd`
