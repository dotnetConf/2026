using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Sourcing.Api;
using SourcingObservabilityLab.ServiceDefaults;
using SupplierDirectory.Api;

namespace SourcingObservabilityLab.Tests;

[Collection("Telemetry")]
public sealed class DatabaseTests
{
    [Fact]
    public async Task RealHttpBoundaryPreservesTraceAndExportsCorrelatedBusinessLogsWithoutContent()
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is Telemetry.SourceName or DatabaseTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var suppliers = new SupplierFactory();
        suppliers.UseKestrel(0);
        using var supplierClient = suppliers.CreateClient();
        using var sourcing = new LabFactory(supplierUrl: supplierClient.BaseAddress!.AbsoluteUri);
        using var client = sourcing.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests",
            new SourcingRequest { Requirements = "PRIVATE-REQUIREMENTS-MARKER managed infrastructure recovery." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<WorkflowResult>())!;
        var directory = Assert.Single(spans, s => s.OperationName == "Supplier.QueryDirectory");
        var database = Assert.Single(spans, s => s.OperationName == "SELECT Suppliers");
        Assert.Equal(result.TraceId, directory.TraceId.ToString());
        Assert.Equal(directory.TraceId, database.TraceId);
        Assert.Equal(directory.SpanId, database.ParentSpanId);
        Assert.Equal(ActivityKind.Client, database.Kind);
        Assert.Equal("sqlite", database.GetTagItem("db.system.name"));
        Assert.All(spans, span => Assert.DoesNotContain("PRIVATE-REQUIREMENTS-MARKER",
            string.Join(" ", span.TagObjects.Select(t => $"{t.Key}={t.Value}"))));
        Assert.Contains(sourcing.Logs.Records, log =>
            log.TraceId == result.TraceId && log.SpanId.Length == 16
            && log.Fields.Contains(result.RequestId.ToString(), StringComparison.Ordinal)
            && log.Fields.Contains("FoundryInvocation", StringComparison.Ordinal)
            && log.Fields.Contains(result.FoundryResponseId, StringComparison.Ordinal));
        Assert.All(sourcing.Logs.Records, log =>
            Assert.DoesNotContain("PRIVATE-REQUIREMENTS-MARKER", log.Fields));
        var events = await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{result.RequestId}/audit");
        Assert.All(events!, e => Assert.Equal(result.TraceId, e.TraceId));
    }

    [Fact]
    public async Task SupplierDirectoryReadsPersistentDatabaseAndDoesNotOverwriteCatalogEditsOnRestart()
    {
        using var factory = new SupplierFactory();
        using (var client = factory.CreateClient())
        {
            var suppliers = await client.GetFromJsonAsync<SupplierDirectory.Api.Supplier[]>("/api/suppliers");
            Assert.Equal(5, suppliers!.Length);
            Assert.Empty((await client.GetFromJsonAsync<SupplierDirectory.Api.Supplier[]>("/api/suppliers?category=Unknown"))!);
            await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath};Pooling=False");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Suppliers SET IsActive=0 WHERE SupplierId='SYN-001';";
            await command.ExecuteNonQueryAsync();
        }
        using var reopened = new SupplierFactory(factory.DatabasePath, ownsDatabase: false);
        using var restartedClient = reopened.CreateClient();
        var active = await restartedClient.GetFromJsonAsync<SupplierDirectory.Api.Supplier[]>(
            "/api/suppliers?category=cloudinfrastructure");
        Assert.Equal(4, active!.Length);
        Assert.DoesNotContain(active, s => s.SupplierId == "SYN-001");
    }

    [Fact]
    public async Task RealSupplierTransportTimeoutHasBoundedDeadlineAndNoAgentInvocation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var slowSupplier = builder.Build();
        slowSupplier.MapGet("/api/suppliers", async (CancellationToken token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15), token);
            return Results.Ok(Array.Empty<Sourcing.Api.Supplier>());
        });
        await slowSupplier.StartAsync();
        try
        {
            using var factory = new LabFactory(supplierUrl: slowSupplier.Urls.Single());
            using var client = factory.CreateClient();
            var timer = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
            Assert.Equal("DependencyTimeout", problem.GetProperty("errorCode").GetString());
            Assert.Equal("SupplierRetrieval", problem.GetProperty("failedStage").GetString());
            Assert.False(problem.GetProperty("agentInvocationCompleted").GetBoolean());
            Assert.True(problem.GetProperty("failureAuditRecorded").GetBoolean());
            Assert.Equal(0, factory.Agent.Calls);
            Assert.InRange(timer.Elapsed.TotalSeconds, 9, 13);
        }
        finally
        {
            await slowSupplier.StopAsync();
        }
    }

    [Theory]
    [InlineData(1, HttpStatusCode.Created, 2, 1)]
    [InlineData(3, HttpStatusCode.BadGateway, 3, 0)]
    public async Task SupplierRetriesAreBoundedAndDoNotRetryTheWorkflow(
        int failures, HttpStatusCode expectedStatus, int expectedAttempts, int expectedAgentCalls)
    {
        var attempts = 0;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var supplier = builder.Build();
        supplier.MapGet("/api/suppliers", (HttpContext context) =>
        {
            if (Interlocked.Increment(ref attempts) <= failures)
            {
                context.Response.Headers.RetryAfter = "0";
                return Results.StatusCode(503);
            }
            return Results.Ok(new[] { new Sourcing.Api.Supplier("SYN-001", "Fictional", ["Managed cloud"], 100000, 900000) });
        });
        await supplier.StartAsync();
        try
        {
            using var factory = new LabFactory(supplierUrl: supplier.Urls.Single());
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());

            Assert.Equal(expectedStatus, response.StatusCode);
            Assert.Equal(expectedAttempts, attempts);
            Assert.Equal(expectedAgentCalls, factory.Agent.Calls);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var id = body.GetProperty("requestId").GetGuid();
            var events = (await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{id}/audit"))!;
            Assert.Single(events, e => e.EventType == "SourcingRequestReceived");
            if (expectedStatus == HttpStatusCode.BadGateway)
            {
                Assert.Equal("SupplierRetrieval", body.GetProperty("failedStage").GetString());
                var stored = await client.GetFromJsonAsync<StoredRequest>($"/api/sourcing/requests/{id}");
                Assert.Equal("Failed", stored!.Status);
                Assert.Empty(stored.Recommendations);
            }
        }
        finally
        {
            await supplier.StopAsync();
        }
    }

    [Fact]
    public async Task ExistingAuditSchemaMigratesWithoutLosingRecordsAndAuditWorksWithoutTraceRecording()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-migration-{Guid.NewGuid():N}.db");
        try
        {
            var id = Guid.NewGuid();
            await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE SourcingRequests (
                        RequestId TEXT PRIMARY KEY,Category TEXT NOT NULL,AnnualSpend TEXT NOT NULL,
                        Currency TEXT NOT NULL,CreatedAt TEXT NOT NULL,Status TEXT NOT NULL,FoundryResponseId TEXT NULL);
                    CREATE TABLE AuditEvents (
                        EventId TEXT PRIMARY KEY,RequestId TEXT NOT NULL,EventType TEXT NOT NULL,
                        Timestamp TEXT NOT NULL,Actor TEXT NOT NULL,SupplierIds TEXT NOT NULL,Outcome TEXT NOT NULL,FailureDetails TEXT NULL);
                    INSERT INTO SourcingRequests VALUES ($id,'CloudInfrastructure','500000','USD',$time,'Processing',NULL);
                    INSERT INTO AuditEvents VALUES ($event,$id,'SourcingRequestReceived',$time,'legacy-actor','[]','Received',NULL);
                    """;
                command.Parameters.AddWithValue("$id", id.ToString());
                command.Parameters.AddWithValue("$event", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Audit:DatabasePath"] = path }).Build();
            var store = new AuditStore(configuration, new TestEnvironment());
            await store.InitializeAsync(CancellationToken.None);
            await store.InitializeAsync(CancellationToken.None);
            Assert.Null(Activity.Current);
            var legacy = Assert.Single(await store.EventsAsync(id, CancellationToken.None));
            Assert.Equal("legacy-actor", legacy.Actor);
            Assert.Null(legacy.TraceId);
            Assert.Null(legacy.FoundryResponseId);
            await store.GeneratedAsync(id, "response-without-telemetry", [], CancellationToken.None);
            await store.CompleteAsync(id, [], false, CancellationToken.None);
            var stored = await store.GetAsync(id, CancellationToken.None);
            Assert.Equal("Completed", stored!.Status);
            var events = await store.EventsAsync(id, CancellationToken.None);
            Assert.Equal(4, events.Count);
            Assert.All(events, e => Assert.Null(e.TraceId));
            Assert.Equal("response-without-telemetry", events.Last().FoundryResponseId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

[CollectionDefinition("Telemetry", DisableParallelization = true)]
public sealed class TelemetryCollection;

internal sealed class SupplierFactory(string? databasePath = null, bool ownsDatabase = true)
    : WebApplicationFactory<SupplierApiProgram>
{
    public string DatabasePath { get; } = databasePath
        ?? Path.Combine(Path.GetTempPath(), $"supplier-directory-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["SupplierCatalog:DatabasePath"] = DatabasePath }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && ownsDatabase) File.Delete(DatabasePath);
    }
}

internal sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "AuditPersistenceTest";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class RecordingLogExporter : BaseExporter<LogRecord>
{
    public ConcurrentBag<(string TraceId, string SpanId, string Fields)> Records { get; } = [];

    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            var fields = new List<string> { record.FormattedMessage ?? "", string.Join(" ", record.Attributes ?? []) };
            record.ForEachScope((scope, state) =>
            {
                foreach (var field in scope) state.Add(field.ToString());
            }, fields);
            Records.Add((record.TraceId.ToString(), record.SpanId.ToString(), string.Join(" ", fields)));
        }
        return ExportResult.Success;
    }
}
