using System.Diagnostics;

namespace SourcingObservabilityLab.ServiceDefaults;

public static class DatabaseTelemetry
{
    public const string SourceName = "SourcingObservabilityLab.Database";
    public static readonly ActivitySource Source = new(SourceName);

    public static async Task<T> RunAsync<T>(string database, string operation, string collection,
        Func<Task<T>> action)
    {
        using var activity = Source.StartActivity($"{operation} {collection}", ActivityKind.Client);
        activity?.SetTag("db.system.name", "sqlite");
        activity?.SetTag("db.namespace", database);
        activity?.SetTag("db.operation.name", operation);
        activity?.SetTag("db.collection.name", collection);
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "DatabaseOperationFailed");
            activity?.SetTag("error.type", ex.GetType().Name);
            throw;
        }
    }
}
