using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SourcingObservabilityLab.ServiceDefaults;

namespace SupplierDirectory.Api;

public sealed record Supplier(string SupplierId, string Name, string[] Capabilities,
    decimal MinimumAnnualSpend, decimal MaximumAnnualSpend);

public sealed class SupplierStore(IConfiguration configuration, IHostEnvironment environment,
    ILogger<SupplierStore> logger)
{
    private static readonly ActivitySource Source = new("SourcingObservabilityLab");
    private readonly string _connectionString = CreateConnectionString(configuration, environment);

    private static string CreateConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var hosted = configuration.GetConnectionString("supplier-catalog");
        var dataSource = string.IsNullOrWhiteSpace(hosted) ? "data/suppliers.db"
            : new SqliteConnectionStringBuilder(hosted).DataSource;
        var path = Path.GetFullPath(configuration["SupplierCatalog:DatabasePath"] ?? dataSource,
            environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 5 }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Suppliers (
                SupplierId TEXT PRIMARY KEY, Name TEXT NOT NULL, Category TEXT NOT NULL,
                Capabilities TEXT NOT NULL, MinimumAnnualSpend TEXT NOT NULL,
                MaximumAnnualSpend TEXT NOT NULL, IsActive INTEGER NOT NULL CHECK(IsActive IN (0,1)));
            CREATE INDEX IF NOT EXISTS IX_Suppliers_Category ON Suppliers(Category,IsActive);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        Supplier[] initialSuppliers =
        [
            new("SYN-001", "Nimbus Grove", ["Managed cloud", "24/7 support", "Disaster recovery"], 300000, 900000),
            new("SYN-002", "Cedar Orbit", ["Managed cloud", "Disaster recovery", "Cost optimization"], 100000, 700000),
            new("SYN-003", "Aurora Harbor", ["Managed cloud", "24/7 support", "Multi-region operations"], 400000, 1500000),
            new("SYN-004", "Copper Meadow", ["Cloud migration", "Business-hours support"], 50000, 300000),
            new("SYN-005", "Silver Fern Systems", ["Managed cloud", "24/7 support", "Disaster recovery", "Cost optimization"], 200000, 1000000)
        ];
        foreach (var supplier in initialSuppliers)
        {
            command.Parameters.Clear();
            command.CommandText = """
                INSERT INTO Suppliers VALUES ($id,$name,'CloudInfrastructure',$capabilities,$min,$max,1)
                ON CONFLICT(SupplierId) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$id", supplier.SupplierId);
            command.Parameters.AddWithValue("$name", supplier.Name);
            command.Parameters.AddWithValue("$capabilities", JsonSerializer.Serialize(supplier.Capabilities));
            command.Parameters.AddWithValue("$min", supplier.MinimumAnnualSpend.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$max", supplier.MaximumAnnualSpend.ToString(CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Supplier[]> GetAsync(string? category, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity("Supplier.QueryDirectory");
        activity?.SetTag("supplier.stage", "CatalogQuery");
        try
        {
            var result = await DatabaseTelemetry.RunAsync("suppliers", "SELECT", "Suppliers", async () =>
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT SupplierId,Name,Capabilities,MinimumAnnualSpend,MaximumAnnualSpend FROM Suppliers
                    WHERE IsActive=1 AND ($category IS NULL OR Category=$category COLLATE NOCASE) ORDER BY SupplierId;
                    """;
                command.Parameters.AddWithValue("$category", string.IsNullOrEmpty(category) ? DBNull.Value : category);
                var suppliers = new List<Supplier>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    suppliers.Add(new(reader.GetString(0), reader.GetString(1),
                        JsonSerializer.Deserialize<string[]>(reader.GetString(2))
                            ?? throw new JsonException("Supplier capabilities are invalid."),
                        decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
                        decimal.Parse(reader.GetString(4), CultureInfo.InvariantCulture)));
                return suppliers.ToArray();
            });
            activity?.SetTag("supplier.result_count", result.Length);
            logger.LogInformation("Active supplier directory queried. Stage {Stage}; SupplierCount {SupplierCount}",
                "CatalogQuery", result.Length);
            return result;
        }
        catch (Exception ex) when (ex is SqliteException or JsonException or FormatException or OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "SupplierDirectoryUnavailable");
            activity?.SetTag("error.type", "SupplierDirectoryUnavailable");
            logger.LogError("Supplier directory query failed. Stage {Stage}; ErrorCode {ErrorCode}; ExceptionType {ExceptionType}",
                "CatalogQuery", "SupplierDirectoryUnavailable", ex.GetType().Name);
            throw;
        }
    }
}
