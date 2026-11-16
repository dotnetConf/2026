using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var supplierDatabase = builder.AddSqlite("supplier-catalog",
    Path.GetFullPath(@"..\Supplier.Api\data", builder.AppHostDirectory), "suppliers.db");
var sourcingDatabase = builder.AddSqlite("sourcing-audit",
    Path.GetFullPath(@"..\Sourcing.Api\data", builder.AppHostDirectory), "sourcing.db");

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DatabaseInspection:Enabled"))
{
    supplierDatabase.WithSqliteWeb();
    sourcingDatabase.WithSqliteWeb();
}

var suppliers = builder.AddProject<Projects.Supplier_Api>("supplier-api", launchProfileName: "http")
    .WithReference(supplierDatabase)
    .WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION", "false")
    .WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION", "false")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.Sourcing_Api>("sourcing-api", launchProfileName: "http")
    .WithReference(sourcingDatabase)
    .WithReference(suppliers)
    .WaitFor(suppliers)
    .WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION", "false")
    .WithEnvironment("OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION", "false")
    .WithEnvironment("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "false")
    .WithEnvironment("AZURE_TRACING_GEN_AI_CONTENT_RECORDING_ENABLED", "false")
    .WithHttpHealthCheck("/health");

builder.Build().Run();
