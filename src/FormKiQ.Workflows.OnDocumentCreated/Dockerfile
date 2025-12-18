FROM public.ecr.aws/lambda/dotnet:10-preview AS base

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["FormKiQ.Workflows.OnDocumentCreated.csproj", "FormKiQ.Workflows.OnDocumentCreated/"]
RUN dotnet restore "FormKiQ.Workflows.OnDocumentCreated/FormKiQ.Workflows.OnDocumentCreated.csproj"

WORKDIR "/src/FormKiQ.Workflows.OnDocumentCreated"
COPY . .
RUN dotnet build "FormKiQ.Workflows.OnDocumentCreated.csproj" --configuration Release --output /app/build

FROM build AS publish
RUN dotnet publish "FormKiQ.Workflows.OnDocumentCreated.csproj" \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained false \
            --output /app/publish \
            -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
CMD ["FormKiQ.Workflows.OnDocumentCreated::FormKiQ.Workflows.OnDocumentCreated.Function::FunctionHandler"]
