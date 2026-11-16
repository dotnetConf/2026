using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sourcing.Api;
using System.ClientModel;
using Azure.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry;

namespace SourcingObservabilityLab.Tests;

[Collection("Telemetry")]
public sealed class WorkflowTests
{
    [Fact]
    public async Task SuccessCommitsRecommendationsAndCompletionAuditAcrossRestart()
    {
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<WorkflowResult>())!;
        Assert.Single(result.Recommendations);
        Assert.Equal(1, factory.Agent.Calls);
        var events = await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{result.RequestId}/audit");
        Assert.Equal(["SourcingRequestReceived", "SupplierRecommendationsGenerated", "RecommendationsPersisted", "SourcingWorkflowCompleted"],
            events!.Select(e => e.EventType).ToArray());

        using var reopened = new LabFactory(databasePath: factory.DatabasePath, ownsDatabase: false);
        using var restartedClient = reopened.CreateClient();
        var stored = await restartedClient.GetFromJsonAsync<StoredRequest>($"/api/sourcing/requests/{result.RequestId}");
        Assert.Equal("Completed", stored!.Status);
        Assert.Single(stored.Recommendations);
        Assert.Equal("resp-test-only", stored.FoundryResponseId);
    }

    [Fact]
    public async Task InjectedAuditFailureRollsBackRecommendationsAndMarksBusinessTraceFailed()
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == Telemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add
        };
        ActivitySource.AddActivityListener(listener);
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, "audit-persistence-failure");
        var response = await client.PostAsJsonAsync("/api/sourcing/requests",
            new SourcingRequest());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("agentInvocationCompleted").GetBoolean());
        Assert.True(problem.GetProperty("recommendationsValidated").GetBoolean());
        Assert.False(problem.GetProperty("recommendationsPersisted").GetBoolean());
        Assert.False(problem.GetProperty("workflowCompleted").GetBoolean());
        Assert.True(problem.GetProperty("failureAuditRecorded").GetBoolean());
        Assert.Equal("AuditPersistence", problem.GetProperty("failedStage").GetString());
        var id = problem.GetProperty("requestId").GetGuid();
        var stored = await client.GetFromJsonAsync<StoredRequest>($"/api/sourcing/requests/{id}");
        Assert.Equal("Failed", stored!.Status);
        Assert.Empty(stored.Recommendations);
        Assert.Equal(1, factory.Agent.Calls);
        var events = (await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{id}/audit"))!;
        Assert.Equal(["SourcingRequestReceived", "SupplierRecommendationsGenerated", "AuditPersistenceFailed"],
            events!.Select(e => e.EventType).ToArray());
        Assert.All(events, e => Assert.Equal(problem.GetProperty("traceId").GetString(), e.TraceId));
        Assert.Null(events[0].FoundryResponseId);
        Assert.All(events.Skip(1), e => Assert.Equal("resp-test-only", e.FoundryResponseId));
        Assert.Contains(spans, s => s.OperationName == "Sourcing.FoundryAgentInvocation" && s.Status == ActivityStatusCode.Ok);
        Assert.Contains(spans, s => s.OperationName == "Sourcing.WriteAuditEvent" && s.Status == ActivityStatusCode.Error);
        Assert.Contains(spans, s => s.OperationName == "Sourcing.ProcessRequest" && s.Status == ActivityStatusCode.Error);
    }

    [Fact]
    public async Task RejectsFailureInjectionOutsideDevelopmentEvenWhenEnabled()
    {
        using var factory = new LabFactory(environment: "Production");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, "audit-persistence-failure");
        var response = await client.PostAsJsonAsync("/api/sourcing/requests",
            new SourcingRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Agent.Calls);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/sourcing/requests/{Guid.NewGuid()}/audit")).StatusCode);
    }

    [Fact]
    public async Task RejectsFailureInjectionWhenDemoDisabled()
    {
        using var factory = new LabFactory(demoEnabled: false);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, "audit-persistence-failure");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sourcing/requests",
            new SourcingRequest())).StatusCode);
        Assert.Equal(0, factory.Agent.Calls);
    }

    [Fact]
    public async Task InvalidModelOutputDoesNotPersistRecommendationsOrCompletion()
    {
        using var factory = new LabFactory(invalidAgentOutput: true);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation", problem.GetProperty("failedStage").GetString());
        Assert.True(problem.GetProperty("agentInvocationCompleted").GetBoolean());
        Assert.False(problem.GetProperty("recommendationsValidated").GetBoolean());
        var id = problem.GetProperty("requestId").GetGuid();
        var stored = await client.GetFromJsonAsync<StoredRequest>($"/api/sourcing/requests/{id}");
        Assert.Empty(stored!.Recommendations);
        var events = await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{id}/audit");
        Assert.DoesNotContain(events!, e => e.EventType == "SourcingWorkflowCompleted");
    }

    [Fact]
    public async Task SwaggerAndInvalidRequestAreAvailableWithoutAzure()
    {
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        Assert.True(spec.GetProperty("paths").TryGetProperty("/api/sourcing/requests", out _));
        Assert.False(spec.GetProperty("components").GetProperty("schemas").GetProperty("SourcingRequest")
            .GetProperty("properties").TryGetProperty("demoScenario", out _));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/sourcing/requests",
            new SourcingRequest { EstimatedAnnualSpend = -1 })).StatusCode);
        Assert.Equal(0, factory.Agent.Calls);
    }

    [Theory]
    [InlineData("unknown")]
    public async Task RejectsUnknownFaultControls(string header)
    {
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, header);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest())).StatusCode);
        Assert.Equal(0, factory.Agent.Calls);
    }

    [Fact]
    public async Task RejectsPrototypePayloadField()
    {
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new
        {
            category = "CloudInfrastructure",
            estimatedAnnualSpend = 500000,
            currency = "USD",
            requirements = "Managed cloud infrastructure with recovery.",
            demoScenario = "none"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Agent.Calls);
    }

    [Theory]
    [InlineData(true, "FoundryAuthenticationFailed")]
    [InlineData(false, "FoundryRequestFailed")]
    public async Task FoundryFailureHasNoResponseIdOrCompletedBusinessState(bool authentication, string code)
    {
        using var factory = new LabFactory(agentError: authentication
            ? new AuthenticationFailedException("PRIVATE-SDK-CONTENT")
            : new ClientResultException("PRIVATE-SDK-CONTENT"));
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("FoundryInvocation", problem.GetProperty("failedStage").GetString());
        Assert.Equal(code, problem.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("foundryResponseId").ValueKind);
        Assert.False(problem.GetProperty("agentInvocationCompleted").GetBoolean());
        Assert.False(problem.GetProperty("recommendationsValidated").GetBoolean());
        var stored = await client.GetFromJsonAsync<StoredRequest>(
            $"/api/sourcing/requests/{problem.GetProperty("requestId").GetGuid()}");
        Assert.Equal("Failed", stored!.Status);
        Assert.Empty(stored.Recommendations);
        Assert.DoesNotContain("PRIVATE-SDK-CONTENT", problem.ToString());
        Assert.All(factory.Logs.Records, log => Assert.DoesNotContain("PRIVATE-SDK-CONTENT", log.Fields));
    }

    [Fact]
    public async Task CompletionMetricCountsOnlyCommittedBusinessTransactionsAndHasNoIdentifierLabels()
    {
        var completions = 0L;
        using var metrics = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "sourcing.completions") listener.EnableMeasurementEvents(instrument);
            }
        };
        metrics.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            Assert.Empty(tags.ToArray());
            completions += value;
        });
        metrics.Start();
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest())).StatusCode);
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, "audit-persistence-failure");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest())).StatusCode);
        Assert.Equal(1, completions);
    }

    [Fact]
    public async Task SupplierFailurePreventsAgentInvocation()
    {
        using var factory = new LabFactory(supplierHandler: () => new CatalogHandler(HttpStatusCode.BadRequest));
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("SupplierRetrieval", problem.GetProperty("failedStage").GetString());
        Assert.True(problem.GetProperty("failureAuditRecorded").GetBoolean());
        Assert.False(problem.GetProperty("agentInvocationCompleted").GetBoolean());
        Assert.Equal(0, factory.Agent.Calls);
    }

    [Fact]
    public async Task FailedRecoveryAuditIsReportedWithoutInventingDurableFailureState()
    {
        using var factory = new LabFactory();
        using var client = factory.CreateClient();
        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath};Pooling=False");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER RejectFailureStatus BEFORE UPDATE OF Status ON SourcingRequests
            WHEN NEW.Status='Failed' BEGIN SELECT RAISE(ABORT,'Failure write unavailable'); END;
            """;
        await command.ExecuteNonQueryAsync();
        client.DefaultRequestHeaders.Add(FailureInjectionHeader.Name, "audit-persistence-failure");
        var response = await client.PostAsJsonAsync("/api/sourcing/requests", new SourcingRequest());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(problem.GetProperty("failureAuditRecorded").GetBoolean());
        var id = problem.GetProperty("requestId").GetGuid();
        var stored = await client.GetFromJsonAsync<StoredRequest>($"/api/sourcing/requests/{id}");
        Assert.Equal("Processing", stored!.Status);
        Assert.Empty(stored.Recommendations);
        var events = await client.GetFromJsonAsync<AuditEvent[]>($"/api/sourcing/requests/{id}/audit");
        Assert.Equal(["SourcingRequestReceived", "SupplierRecommendationsGenerated"], events!.Select(e => e.EventType));
    }

    [Fact]
    public async Task CallerCancellationIsDistinctFromDependencyTimeout()
    {
        using var factory = new LabFactory();
        _ = factory.CreateClient();
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new CatalogHandler());
        var workflow = new SourcingWorkflow(http, factory.Agent,
            factory.Services.GetRequiredService<AuditStore>(),
            factory.Services.GetRequiredService<ILogger<SourcingWorkflow>>());
        cancellation.Cancel();
        var failure = await Assert.ThrowsAsync<WorkflowException>(() => workflow.RunAsync(new(), cancellation.Token));
        Assert.Equal("RequestCancelled", failure.Message);
        Assert.Equal(499, failure.Status);
    }
}

internal sealed class LabFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly bool _demoEnabled;
    private readonly bool _ownsDatabase;
    private readonly string? _supplierUrl;
    private readonly Func<HttpMessageHandler>? _supplierHandler;
    public string DatabasePath { get; }
    public TestAgent Agent { get; }
    public RecordingLogExporter Logs { get; } = new();

    public LabFactory(string environment = "Development", bool demoEnabled = true, bool invalidAgentOutput = false,
        string? databasePath = null, bool ownsDatabase = true, Exception? agentError = null,
        string? supplierUrl = null, Func<HttpMessageHandler>? supplierHandler = null)
    {
        _environment = environment;
        _demoEnabled = demoEnabled;
        _ownsDatabase = ownsDatabase;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"sourcing-lab-{Guid.NewGuid():N}.db");
        Agent = new TestAgent(invalidAgentOutput, agentError);
        _supplierUrl = supplierUrl;
        _supplierHandler = supplierHandler;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Audit:DatabasePath"] = DatabasePath,
            ["Demo:Enabled"] = _demoEnabled.ToString(),
            ["SupplierApi:BaseUrl"] = _supplierUrl ?? "http://supplier-api"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddLogging(logging => logging.AddOpenTelemetry(options =>
                options.AddProcessor(new SimpleLogRecordExportProcessor(Logs))));
            services.RemoveAll<IRecommendationAgent>();
            services.AddSingleton<IRecommendationAgent>(Agent);
            if (_supplierUrl is null)
                services.AddHttpClient<SourcingWorkflow>().ConfigurePrimaryHttpMessageHandler(
                    _supplierHandler ?? (() => new CatalogHandler()));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase && File.Exists(DatabasePath)) File.Delete(DatabasePath);
    }
}

internal sealed class TestAgent(bool invalid, Exception? error = null) : IRecommendationAgent
{
    public int Calls { get; private set; }

    public Task<AgentReply> RecommendAsync(Guid requestId, SourcingRequest request,
        IReadOnlyList<Supplier> suppliers, CancellationToken cancellationToken)
    {
        Calls++;
        if (error is not null) throw error;
        var result = new AgentOutput(requestId, [new(invalid ? "UNKNOWN" : suppliers[0].SupplierId, 1, "Synthetic test recommendation.")]);
        return Task.FromResult(new AgentReply("resp-test-only", JsonSerializer.Serialize(result)));
    }
}

internal sealed class CatalogHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(status)
        {
            Content = JsonContent.Create(new[] { new Supplier("SYN-001", "Fictional", ["Managed cloud"], 100000, 900000) })
        });
}
