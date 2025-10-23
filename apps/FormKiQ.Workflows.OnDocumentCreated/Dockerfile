#FROM public.ecr.aws/lambda/dotnet:8 AS base
#WORKDIR /var/task
#COPY ["FormKiQ.Workflows.Functions.DocumentCreated.csproj", "./FormKiQ.Workflows.Functions.DocumentCreated/"]
#WORKDIR /var/task/FormKiQ.Workflows.Functions.DocumentCreated
#RUN dotnet restore
#
#FROM base AS build
#WORKDIR /src
#COPY . .
#WORKDIR /src/FormKiQ.Workflows.Functions.DocumentCreated
#RUN dotnet build "FormKiQ.Workflows.Functions.DocumentCreated.csproj" --configuration Release --output /app/build
#
#FROM build AS publish
#RUN dotnet publish "FormKiQ.Workflows.Functions.DocumentCreated.csproj" --configuration Release --output /app/publish /p:UseAppHost=false
#
#FROM base AS final
#WORKDIR /var/task
#COPY --from=publish /app/publish .
#CMD ["FormKiQ.Workflows.Functions.DocumentCreated::FormKiQ.Workflows.Functions.DocumentCreated.Function::FunctionHandler"]

FROM public.ecr.aws/lambda/dotnet:8 AS base

FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
WORKDIR /src
COPY ["FormKiQ.Workflows.Functions.DocumentCreated.csproj", "FormKiQ.Workflows.Functions.DocumentCreated/"]
RUN dotnet restore "FormKiQ.Workflows.Functions.DocumentCreated/FormKiQ.Workflows.Functions.DocumentCreated.csproj"

WORKDIR "/src/FormKiQ.Workflows.Functions.DocumentCreated"
COPY . .
RUN dotnet build "FormKiQ.Workflows.Functions.DocumentCreated.csproj" --configuration Release --output /app/build

FROM build AS publish
RUN dotnet publish "FormKiQ.Workflows.Functions.DocumentCreated.csproj" \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            --output /app/publish \
            -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /var/task
COPY --from=publish /app/publish .
