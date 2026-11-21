using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Metrics;
using Sourcing.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    options.Retry.MaxRetryAttempts = 2;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
});
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(Telemetry.SourceName));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
{
    Title = "Procurement Sourcing API",
    Version = "v1",
    Description = "Evaluate supplier candidates, validate recommendations, and commit the procurement audit trail."
}));
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Demo:Enabled"))
    builder.Services.Configure<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>(
        options => options.OperationFilter<FailureInjectionHeader>());
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString());
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(
    options => options.ThrowOnBadRequest = false);
builder.Services.AddOptions<FoundryOptions>().BindConfiguration("Foundry")
    .Validate(o => Uri.TryCreate(o.ProjectEndpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == "https", "Foundry:ProjectEndpoint must be an absolute HTTPS URI.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.AgentName) && !string.IsNullOrWhiteSpace(o.AgentVersion),
        "Foundry:AgentName and Foundry:AgentVersion are required.")
    .Validate(o => o.TimeoutSeconds is >= 1 and <= 300, "Foundry timeout must be between 1 and 300 seconds.")
    .ValidateOnStart();
builder.Services.AddSingleton<IRecommendationAgent, FoundryAgent>();
builder.Services.AddSingleton<AuditStore>();
builder.Services.AddHttpClient<SourcingWorkflow>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SupplierApi:BaseUrl"] ?? "http://supplier-api");
    client.Timeout = Timeout.InfiniteTimeSpan;
});
var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.Services.GetRequiredService<AuditStore>().InitializeAsync(app.Lifetime.ApplicationStopping);

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapPost("/api/sourcing/requests", async (SourcingRequest request, HttpRequest httpRequest, SourcingWorkflow workflow,
    CancellationToken cancellationToken) =>
{
    var errors = new List<ValidationResult>();
    Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
    var failureHeader = httpRequest.Headers[FailureInjectionHeader.Name];
    if (failureHeader.Count != 0 && (!app.Environment.IsDevelopment()
        || !app.Configuration.GetValue<bool>("Demo:Enabled")
        || failureHeader.Count != 1 || failureHeader[0] != "audit-persistence-failure"))
        errors.Add(new("The requested fault injection is unavailable.", [FailureInjectionHeader.Name]));
    if (errors.Count != 0)
        return Results.ValidationProblem(errors.SelectMany(e => e.MemberNames.DefaultIfEmpty("request")
            .Select(name => (name, message: e.ErrorMessage ?? "Invalid request.")))
            .GroupBy(e => e.name).ToDictionary(g => g.Key, g => g.Select(e => e.message).ToArray()));
    try
    {
        var result = await workflow.RunAsync(request, cancellationToken, failureHeader.Count != 0);
        return Results.Created($"/api/sourcing/requests/{result.RequestId}", result);
    }
    catch (WorkflowException ex)
    {
        return Results.Problem(statusCode: ex.Status,
            title: ex.Stage == "AuditPersistence" ? "Required business audit persistence failed" : "Sourcing workflow failed",
            detail: "The business operation did not complete. Inspect the request audit events and application trace.",
            extensions: new Dictionary<string, object?>
            {
                ["requestId"] = ex.RequestId,
                ["traceId"] = ex.TraceId,
                ["foundryResponseId"] = ex.FoundryResponseId,
                ["failedStage"] = ex.Stage,
                ["errorCode"] = ex.Message,
                ["agentInvocationCompleted"] = ex.AgentCompleted,
                ["recommendationsValidated"] = ex.Validated,
                ["recommendationsPersisted"] = false,
                ["workflowCompleted"] = false,
                ["failureAuditRecorded"] = ex.FailureAuditRecorded
            });
    }
})
.WithSummary("Evaluate supplier candidates and commit a validated sourcing recommendation.")
.Produces<WorkflowResult>(201).ProducesValidationProblem().ProducesProblem(502).ProducesProblem(503).ProducesProblem(504);

app.MapGet("/api/sourcing/requests/{id:guid}", async (Guid id, AuditStore store, CancellationToken cancellationToken) =>
{
    var result = await store.GetAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
}).Produces<StoredRequest>().Produces(404);

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/sourcing/requests/{id:guid}/audit",
        async (Guid id, AuditStore store, CancellationToken cancellationToken) =>
            await store.GetAsync(id, cancellationToken) is null
                ? Results.NotFound()
                : Results.Ok(await store.EventsAsync(id, cancellationToken)))
        .WithSummary("Read the sourcing request's committed business audit trail (local development only).")
        .Produces<List<AuditEvent>>().Produces(404);
}

app.MapDefaultEndpoints();
app.Run();
