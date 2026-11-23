using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SourcingObservabilityLab.ServiceDefaults;

namespace Sourcing.Api;

public sealed class AuditPersistenceException(string message) : Exception(message);

public sealed class AuditStore
{
    private readonly string _connectionString;

    public AuditStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var hosted = configuration.GetConnectionString("sourcing-audit");
        var dataSource = string.IsNullOrWhiteSpace(hosted) ? "data/sourcing.db"
            : new SqliteConnectionStringBuilder(hosted).DataSource;
        var path = Path.GetFullPath(configuration["Audit:DatabasePath"] ?? dataSource, environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS SourcingRequests (
                RequestId TEXT PRIMARY KEY, Category TEXT NOT NULL, AnnualSpend TEXT NOT NULL,
                Currency TEXT NOT NULL, CreatedAt TEXT NOT NULL, Status TEXT NOT NULL,
                FoundryResponseId TEXT NULL);
            CREATE TABLE IF NOT EXISTS Recommendations (
                RequestId TEXT NOT NULL REFERENCES SourcingRequests(RequestId),
                SupplierId TEXT NOT NULL, Rank INTEGER NOT NULL CHECK(Rank BETWEEN 1 AND 3),
                Explanation TEXT NOT NULL, PRIMARY KEY(RequestId, SupplierId), UNIQUE(RequestId, Rank));
            CREATE TABLE IF NOT EXISTS AuditEvents (
                EventId TEXT PRIMARY KEY, RequestId TEXT NOT NULL REFERENCES SourcingRequests(RequestId),
                EventType TEXT NOT NULL, Timestamp TEXT NOT NULL, Actor TEXT NOT NULL,
                SupplierIds TEXT NOT NULL, Outcome TEXT NOT NULL, FailureDetails TEXT NULL);
            CREATE INDEX IF NOT EXISTS IX_AuditEvents_RequestId ON AuditEvents(RequestId);
            """, cancellationToken);
        using var columns = connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(AuditEvents);";
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await columns.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) names.Add(reader.GetString(1));
        }
        using var migration = connection.BeginTransaction();
        foreach (var name in new[] { "TraceId", "SpanId", "FoundryResponseId" })
        {
            if (!names.Contains(name))
                await ExecuteAsync(connection, migration, $"ALTER TABLE AuditEvents ADD COLUMN {name} TEXT NULL;",
                    cancellationToken);
        }
        await migration.CommitAsync(cancellationToken);
    }

    public Task ReceiveAsync(Guid id, SourcingRequest request, CancellationToken cancellationToken) =>
        WriteCheckpointAsync("Received", async () =>
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(connection, transaction, """
            INSERT INTO SourcingRequests VALUES ($id, $category, $spend, $currency, $time, 'Processing', NULL);
            """, cancellationToken, ("$id", id.ToString()), ("$category", request.Category),
            ("$spend", request.EstimatedAnnualSpend.ToString(CultureInfo.InvariantCulture)),
            ("$currency", request.Currency), ("$time", DateTimeOffset.UtcNow.ToString("O")));
        await WriteEventAsync(connection, transaction, id, "SourcingRequestReceived", [], "Received", null, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
    });

    public Task GeneratedAsync(Guid id, string responseId, Recommendation[] recommendations, CancellationToken cancellationToken) =>
        WriteCheckpointAsync("RecommendationsValidated", async () =>
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(connection, transaction,
            "UPDATE SourcingRequests SET FoundryResponseId=$response WHERE RequestId=$id;",
            cancellationToken, ("$id", id.ToString()), ("$response", responseId));
        await WriteEventAsync(connection, transaction, id, "SupplierRecommendationsGenerated",
            recommendations.Select(r => r.SupplierId).ToArray(), "Validated", null, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
    });

    public async Task CompleteAsync(Guid id, Recommendation[] recommendations, bool failAudit, CancellationToken cancellationToken)
    {
        using var persistence = Telemetry.Source.StartActivity("Sourcing.PersistRecommendations");
        persistence?.SetTag("sourcing.stage", "CommitRecommendations");
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            foreach (var recommendation in recommendations)
            {
                await ExecuteAsync(connection, transaction,
                    "INSERT INTO Recommendations VALUES ($id,$supplier,$rank,$explanation);", cancellationToken,
                    ("$id", id.ToString()), ("$supplier", recommendation.SupplierId),
                    ("$rank", recommendation.Rank), ("$explanation", recommendation.Explanation));
            }

            using (var audit = Telemetry.Source.StartActivity("Sourcing.WriteAuditEvent"))
            {
                audit?.SetTag("sourcing.checkpoint", "CompletionRequired");
                try
                {
                    if (failAudit)
                        throw new AuditPersistenceException("The required audit write failed before transaction commit.");
                    var suppliers = recommendations.Select(r => r.SupplierId).ToArray();
                    await WriteEventAsync(connection, transaction, id, "RecommendationsPersisted", suppliers, "Succeeded", null, cancellationToken);
                    await WriteEventAsync(connection, transaction, id, "SourcingWorkflowCompleted", suppliers, "Succeeded", null, cancellationToken);
                }
                catch (Exception ex) when (ex is AuditPersistenceException or SqliteException or OperationCanceledException)
                {
                    Telemetry.Error(audit, "RequiredAuditWriteFailed");
                    throw;
                }
            }

            await ExecuteAsync(connection, transaction, "UPDATE SourcingRequests SET Status='Completed' WHERE RequestId=$id;",
                cancellationToken, ("$id", id.ToString()));
            await CommitAsync(transaction, cancellationToken);
        }
        catch (Exception ex) when (ex is AuditPersistenceException or SqliteException or OperationCanceledException)
        {
            Telemetry.Error(persistence, "PersistenceFailed");
            throw;
        }
    }

    public Task FailAsync(Guid id, string eventType, string code, CancellationToken cancellationToken,
        string? responseId = null) =>
        WriteCheckpointAsync("FailureRecorded", async () =>
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(connection, transaction, """
            UPDATE SourcingRequests SET Status='Failed',FoundryResponseId=COALESCE($response,FoundryResponseId)
            WHERE RequestId=$id;
            """, cancellationToken, ("$id", id.ToString()), ("$response", responseId));
        await WriteEventAsync(connection, transaction, id, eventType, [], "Failed", code, cancellationToken);
        await CommitAsync(transaction, cancellationToken);
    });

    public Task<StoredRequest?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        DatabaseTelemetry.RunAsync<StoredRequest?>("sourcing", "SELECT", "SourcingRequests", async () =>
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Status, FoundryResponseId FROM SourcingRequests WHERE RequestId=$id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        string status;
        string? responseId;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            status = reader.GetString(0);
            responseId = reader.IsDBNull(1) ? null : reader.GetString(1);
        }
        command.CommandText = "SELECT SupplierId,Rank,Explanation FROM Recommendations WHERE RequestId=$id ORDER BY Rank;";
        var recommendations = new List<Recommendation>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                recommendations.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
        }
        return new StoredRequest(id, status, responseId, recommendations.ToArray());
    });

    public Task<List<AuditEvent>> EventsAsync(Guid id, CancellationToken cancellationToken) =>
        DatabaseTelemetry.RunAsync("sourcing", "SELECT", "AuditEvents", async () =>
    {
        await using var connection = await OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EventId,EventType,Timestamp,Actor,SupplierIds,Outcome,FailureDetails,TraceId,SpanId,FoundryResponseId FROM AuditEvents WHERE RequestId=$id ORDER BY rowid;";
        command.Parameters.AddWithValue("$id", id.ToString());
        var events = new List<AuditEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            events.Add(new(Guid.Parse(reader.GetString(0)), id, reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture), reader.GetString(3),
                JsonSerializer.Deserialize<string[]>(reader.GetString(4))!, reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        return events;
    });

    private static async Task WriteCheckpointAsync(string checkpoint, Func<Task> action)
    {
        using var activity = Telemetry.Source.StartActivity("Sourcing.WriteAuditEvent");
        activity?.SetTag("sourcing.checkpoint", checkpoint);
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is SqliteException or OperationCanceledException)
        {
            Telemetry.Error(activity, "AuditPersistenceFailed");
            throw;
        }
    }

    private static Task CommitAsync(SqliteTransaction transaction, CancellationToken cancellationToken) =>
        DatabaseTelemetry.RunAsync("sourcing", "COMMIT", "SourcingRequests", async () =>
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        });

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static Task WriteEventAsync(SqliteConnection connection, SqliteTransaction transaction,
        Guid id, string type, string[] suppliers, string outcome, string? details, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, """
            INSERT INTO AuditEvents (EventId,RequestId,EventType,Timestamp,Actor,SupplierIds,Outcome,FailureDetails,TraceId,SpanId,FoundryResponseId)
            VALUES ($event,$id,$type,$time,$actor,$suppliers,$outcome,$details,$trace,$span,
                (SELECT FoundryResponseId FROM SourcingRequests WHERE RequestId=$id));
            """,
            cancellationToken, ("$event", Guid.NewGuid().ToString()), ("$id", id.ToString()), ("$type", type),
            ("$time", DateTimeOffset.UtcNow.ToString("O")), ("$actor", "sourcing-workflow"),
            ("$suppliers", JsonSerializer.Serialize(suppliers)), ("$outcome", outcome), ("$details", details),
            ("$trace", Activity.Current?.TraceId.ToString()), ("$span", Activity.Current?.SpanId.ToString()));

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var operation = sql.TrimStart().Split(' ', '\r', '\n')[0];
        var collection = sql.Contains("AuditEvents", StringComparison.Ordinal) ? "AuditEvents"
            : sql.Contains("Recommendations", StringComparison.Ordinal) ? "Recommendations" : "SourcingRequests";
        await DatabaseTelemetry.RunAsync("sourcing", operation, collection,
            () => command.ExecuteNonQueryAsync(cancellationToken));
    }
}
