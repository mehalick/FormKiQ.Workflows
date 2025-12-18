dotnet build ./../FormKiQ.Workflows.sln  -c Release
cdk deploy --require-approval never

#dotnet publish ./../src/FormKiQ.App/FormKiQ.App.csproj -c Release
#aws s3 sync "..\src\FormKiQ.App\bin\Release\net10.0\publish\wwwroot" s3://formkiq-core-prod-webapp-360691803289