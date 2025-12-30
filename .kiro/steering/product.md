# Product Overview

FormKiQ.Workflows is a serverless workflow automation system built on AWS Lambda. The project provides event-driven document processing workflows that respond to document creation events in the FormKiQ document management platform.

## Core Components

### Document Processing Pipeline
- **OnDocumentCreated Lambda**: Processes documents when they are uploaded to the system
- **Image Processing**: Resizes and optimizes images using SixLabors.ImageSharp
- **Text Extraction**: Extracts text from documents using AWS Textract
- **Label Processing**: Analyzes and categorizes documents using AWS Rekognition
- **Slack Integration**: Sends notifications to Slack channels for workflow events

### Applications
- **FormKiQ.App**: Blazor WebAssembly application for document management UI
- **FormKiQ.Reporting**: Console application for generating reports
- **FormKiQ.Uploader**: Utility for bulk document uploads

### Infrastructure
- **AWS Lambda**: Serverless compute for document processing workflows
- **Amazon SQS**: Message queuing for reliable event processing
- **Amazon S3**: Object storage for documents and processed artifacts
- **AWS CDK**: Infrastructure as Code for deployment automation

## Architecture Benefits
- **Serverless**: Pay-per-use pricing with automatic scaling
- **Event-Driven**: Responsive processing triggered by document events
- **Resilient**: SQS batch processing with retry mechanisms
- **Observable**: Comprehensive logging with AWS Lambda Powertools
