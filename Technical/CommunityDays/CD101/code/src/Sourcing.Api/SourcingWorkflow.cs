using System.ClientModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Data.Sqlite;
using Polly.Timeout;

namespace Sourcing.Api;

public sealed class WorkflowException(
    Guid requestId, string traceId, string? foundryResponseId, string stage, int status,
    string code, bool agentCompleted, bool validated, bool failureAuditRecorded) : Exception(code)
{
    public Guid RequestId { get; } = requestId;
    public string TraceId { get; } = traceId;
    public string? FoundryResponseId { get; } = foundryResponseId;
    public string Stage { get; } = stage;
    public int Status { get; } = status;
    public bool AgentCompleted { get; } = agentCompleted;
    public bool Validated { get; } = validated;
    public bool FailureAuditRecorded { get; } = failureAuditRecorded;
}

public sealed class SourcingWorkflow(HttpClient suppliers, IRecommendationAgent agent, AuditStore store,
    ILogger<SourcingWorkflow> logger)
{
    public async Task<WorkflowResult> RunAsync(SourcingRequest request, CancellationToken cancellationToken,
        bool failAudit = false)
    {
        var id = Guid.NewGuid();
        using var operation = Telemetry.Source.StartActivity("Sourcing.ProcessRequest");
        var traceId = Activity.Current?.TraceId.ToString() ?? "";
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["SourcingRequestId"] = id,
            ["ApplicationTraceId"] = traceId
        });
        operation?.SetTag("sourcing.request.id", id.ToString());
        operation?.SetTag("sourcing.category", request.Category);
        operation?.SetTag("sourcing.spend_band", request.EstimatedAnnualSpend < 1000000 ? "under-1m" : "1m-plus");
        var start = Stopwatch.GetTimestamp();
        var stage = "ReceiveRequest";
        var received = false;
        var agentCompleted = false;
        var validated = false;
        string? responseId = null;
        Telemetry.Requests.Add(1);
        try
        {
            await store.ReceiveAsync(id, request, cancellationToken);
            received = true;
            logger.LogInformation("Procurement request accepted. Stage {Stage}; Category {Category}", stage, request.Category);

            stage = "SupplierRetrieval";
            var candidates = await InStageAsync("Sourcing.GetSuppliers", async () =>
                await suppliers.GetFromJsonAsync<Supplier[]>(
                    $"api/suppliers?category={Uri.EscapeDataString(request.Category)}", cancellationToken)
                ?? throw new JsonException("Supplier API returned no catalog."));
            if (candidates.Length == 0)
                throw new RecommendationValidationException("No supplier candidates available for this category.");
            operation?.SetTag("sourcing.supplier_count", candidates.Length);
            logger.LogInformation("Supplier shortlist retrieved. Stage {Stage}; SupplierCount {SupplierCount}",
                stage, candidates.Length);

            stage = "FoundryInvocation";
            var reply = await InStageAsync("Sourcing.FoundryAgentInvocation", async () =>
            {
                var result = await agent.RecommendAsync(id, request, candidates, cancellationToken);
                Activity.Current?.SetTag("foundry.response.id", result.ResponseId);
                return result;
            });
            agentCompleted = true;
            responseId = reply.ResponseId;
            operation?.SetTag("foundry.response.id", responseId);
            logger.LogInformation("Supplier recommendation received. Stage {Stage}; FoundryResponseId {FoundryResponseId}", stage, responseId);

            stage = "Validation";
            var recommendations = await InStageAsync("Sourcing.ValidateRecommendations", () =>
                Task.FromResult(RecommendationValidator.Validate(reply.Json, id, candidates)));
            validated = true;
            logger.LogInformation("Supplier recommendations passed business validation. Stage {Stage}; RecommendationCount {RecommendationCount}; FoundryResponseId {FoundryResponseId}",
                stage, recommendations.Length, responseId);

            stage = "AuditPersistence";
            await store.GeneratedAsync(id, responseId, recommendations, cancellationToken);
            await store.CompleteAsync(id, recommendations, failAudit, cancellationToken);
            Telemetry.Completions.Add(1);
            operation?.SetStatus(ActivityStatusCode.Ok);
            logger.LogInformation("Procurement recommendations and required audit trail committed. Stage {Stage}; Outcome {Outcome}; FoundryResponseId {FoundryResponseId}",
                stage, "Completed", responseId);
            return new(id, "Completed", traceId, responseId, recommendations);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or RecommendationValidationException
            or AuditPersistenceException or SqliteException or ClientResultException
            or AuthenticationFailedException or OperationCanceledException or TimeoutRejectedException)
        {
            var code = ex switch
            {
                AuditPersistenceException => "RequiredAuditWriteFailed",
                SqliteException => "DatabasePersistenceFailed",
                OperationCanceledException when cancellationToken.IsCancellationRequested => "RequestCancelled",
                OperationCanceledException => "DependencyTimeout",
                TimeoutRejectedException => "DependencyTimeout",
                RecommendationValidationException => "RecommendationValidationFailed",
                AuthenticationFailedException => "FoundryAuthenticationFailed",
                ClientResultException => "FoundryRequestFailed",
                _ => "DependencyFailed"
            };
            Telemetry.Error(operation, code);
            Telemetry.Failures.Add(1, new KeyValuePair<string, object?>("stage", stage));
            // SDK exception messages can contain response bodies, so log only safe classifications.
            logger.LogError("Procurement request could not be completed. Stage {Stage}; ErrorCode {ErrorCode}; ExceptionType {ExceptionType}; AgentCompleted {AgentCompleted}; Validated {Validated}; FoundryResponseId {FoundryResponseId}",
                stage, code, ex.GetType().Name, agentCompleted, validated, responseId);
            var recorded = false;
            if (received)
            {
                using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await store.FailAsync(id, stage == "AuditPersistence" ? "AuditPersistenceFailed" : "SourcingWorkflowFailed",
                        code, recovery.Token, responseId);
                    recorded = true;
                }
                catch (Exception recoveryError) when (recoveryError is SqliteException or OperationCanceledException)
                {
                    logger.LogError("Failure audit could not be committed. Stage {Stage}; ErrorCode {ErrorCode}; ExceptionType {ExceptionType}; FoundryResponseId {FoundryResponseId}",
                        "FailureAuditPersistence", "FailureAuditWriteFailed", recoveryError.GetType().Name, responseId);
                }
            }
            var status = ex is OperationCanceledException && cancellationToken.IsCancellationRequested ? 499
                : ex is OperationCanceledException or TimeoutRejectedException ? 504
                : stage is "AuditPersistence" or "ReceiveRequest" ? 503 : 502;
            throw new WorkflowException(id, traceId, responseId, stage, status, code, agentCompleted, validated, recorded);
        }
        finally
        {
            Telemetry.Duration.Record(Stopwatch.GetElapsedTime(start).TotalSeconds);
        }
    }

    private static async Task<T> InStageAsync<T>(string name, Func<Task<T>> action)
    {
        using var activity = Telemetry.Source.StartActivity(name);
        activity?.SetTag("sourcing.stage", name["Sourcing.".Length..]);
        try
        {
            var result = await action();
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            Telemetry.Error(activity, ex.GetType().Name);
            throw;
        }
    }
}
