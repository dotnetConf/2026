using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace ReleasePlannerServer;

[McpServerToolType]
public static class LegacyReleasePlannerTools
{
    [McpServerTool(
        Name = "plan_release_legacy",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Build a production release plan through two stateful ElicitAsync calls.")]
    public static async Task<string> PlanReleaseLegacyAsync(
        McpServer server,
        [Description("Application or service to release")] string application,
        [Description("Version or build identifier to release")] string version,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (server.ClientCapabilities?.Elicitation?.Form is null)
        {
            return "This client cannot complete the interactive release plan because it does not support form elicitation.";
        }

        ElicitResult detailsResult = await server.ElicitAsync(
            new ElicitRequestParams
            {
                Message = $"Release controls for {application} {version}",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["environment"] = new ElicitRequestParams.UntitledSingleSelectEnumSchema
                        {
                            Title = "Target environment",
                            Enum = ["staging", "production"],
                            Default = "production",
                        },
                        ["strategy"] = new ElicitRequestParams.UntitledSingleSelectEnumSchema
                        {
                            Title = "Rollout strategy",
                            Enum = ["canary", "blue-green", "rolling"],
                            Default = "canary",
                        },
                        ["initialRolloutPercent"] = new ElicitRequestParams.NumberSchema
                        {
                            Type = "integer",
                            Title = "Initial rollout percentage",
                            Minimum = 1,
                            Maximum = 100,
                            Default = 10,
                        },
                        ["maxErrorRatePercent"] = new ElicitRequestParams.NumberSchema
                        {
                            Title = "Rollback error-rate threshold",
                            Description = "Abort the rollout when the observed error rate exceeds this percentage",
                            Minimum = 0,
                            Maximum = 100,
                            Default = 1.0,
                        },
                    },
                    Required = ["environment", "strategy", "initialRolloutPercent", "maxErrorRatePercent"],
                },
            },
            cancellationToken);

        if (!detailsResult.IsAccepted)
        {
            return $"Release planning {detailsResult.Action}.";
        }

        IDictionary<string, JsonElement> details =
            detailsResult.Content ?? throw new McpException("The accepted release-details form was empty.");
        string environment = GetRequiredChoice(details, "environment", "staging", "production");
        string strategy = GetRequiredChoice(details, "strategy", "canary", "blue-green", "rolling");
        int initialRolloutPercent = GetRequiredInt32(details, "initialRolloutPercent", 1, 100);
        double maxErrorRatePercent = GetRequiredDouble(details, "maxErrorRatePercent", 0, 100);

        ElicitResult approvalResult = await server.ElicitAsync(
            new ElicitRequestParams
            {
                Message =
                    $"Approve a {strategy} release of {application} {version} to {environment}, " +
                    $"starting at {initialRolloutPercent}% traffic with rollback above " +
                    $"{maxErrorRatePercent:0.##}% errors?",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        ["approved"] = new ElicitRequestParams.BooleanSchema
                        {
                            Title = "Approve release plan",
                            Default = true,
                        },
                    },
                    Required = ["approved"],
                },
            },
            cancellationToken);

        if (!approvalResult.IsAccepted)
        {
            return $"Release-plan approval {approvalResult.Action}.";
        }

        IDictionary<string, JsonElement> approval =
            approvalResult.Content ?? throw new McpException("The accepted approval form was empty.");
        if (!GetRequiredBoolean(approval, "approved"))
        {
            return "Release plan was not approved.";
        }

        return $"""
            RELEASE PLAN: {application} {version}
            Target: {environment}
            Strategy: {strategy}
            Initial rollout: {initialRolloutPercent}%
            Automatic rollback threshold: {maxErrorRatePercent:0.##}% errors

            00:00  Verify signed artifact, configuration, and database compatibility
            00:05  Route {initialRolloutPercent}% of traffic to {version}
            00:15  Evaluate latency, errors, and saturation against health gates
            00:20  Continue the {strategy} rollout when all gates pass

            Rollback: restore the previous version when errors exceed {maxErrorRatePercent:0.##}%.

            Legacy proof: the handler remained alive across two ElicitAsync callbacks.
            """;
    }

    private static string GetRequiredChoice(
        IDictionary<string, JsonElement> content,
        string name,
        params string[] allowedValues)
    {
        if (!content.TryGetValue(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new McpException($"The elicitation response field '{name}' must be a string.");
        }

        string result = value.GetString()!;
        if (!allowedValues.Contains(result, StringComparer.Ordinal))
        {
            throw new McpException($"The elicitation response field '{name}' has an unsupported value.");
        }

        return result;
    }

    private static int GetRequiredInt32(
        IDictionary<string, JsonElement> content,
        string name,
        int minimum,
        int maximum)
    {
        if (!content.TryGetValue(name, out JsonElement value) || !value.TryGetInt32(out int result))
        {
            throw new McpException($"The elicitation response field '{name}' must be an integer.");
        }

        if (result < minimum || result > maximum)
        {
            throw new McpException($"The elicitation response field '{name}' must be between {minimum} and {maximum}.");
        }

        return result;
    }

    private static double GetRequiredDouble(
        IDictionary<string, JsonElement> content,
        string name,
        double minimum,
        double maximum)
    {
        if (!content.TryGetValue(name, out JsonElement value) ||
            !value.TryGetDouble(out double result) ||
            !double.IsFinite(result))
        {
            throw new McpException($"The elicitation response field '{name}' must be a finite number.");
        }

        if (result < minimum || result > maximum)
        {
            throw new McpException($"The elicitation response field '{name}' must be between {minimum} and {maximum}.");
        }

        return result;
    }

    private static bool GetRequiredBoolean(IDictionary<string, JsonElement> content, string name)
    {
        if (!content.TryGetValue(name, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new McpException($"The elicitation response field '{name}' must be a Boolean.");
        }

        return value.GetBoolean();
    }
}
