using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Sourcing.Api;

public static class Telemetry
{
    public const string SourceName = "SourcingObservabilityLab";
    public static readonly ActivitySource Source = new(SourceName);
    public static readonly Meter Meter = new(SourceName);
    public static readonly Counter<long> Requests = Meter.CreateCounter<long>("sourcing.requests");
    public static readonly Counter<long> Failures = Meter.CreateCounter<long>("sourcing.failures");
    public static readonly Counter<long> Completions = Meter.CreateCounter<long>("sourcing.completions");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("sourcing.duration", "s");

    public static void Error(Activity? activity, string code)
    {
        activity?.SetStatus(ActivityStatusCode.Error, code);
        activity?.SetTag("error.type", code);
    }
}
