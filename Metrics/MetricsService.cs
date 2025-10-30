using System.Diagnostics.Metrics;

namespace MetricsDemo.Metrics;

public class MetricsService
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _executionTime;

    public MetricsService()
    {
        _meter = new Meter("MetricsDemoApp", "1.0.0");

        _requestCounter = _meter.CreateCounter<long>(
            name: "demo_requests_total",
            unit: "requests",
            description: "Total number of requests per endpoint");

        _executionTime = _meter.CreateHistogram<double>(
            name: "demo_request_duration_ms",
            unit: "ms",
            description: "Request execution time in milliseconds");
    }

    public void RecordRequest(string endpoint, IDictionary<string, object>? parameters = null, double? durationMs = null)
    {
        var tags = new List<KeyValuePair<string, object?>>() 
        { 
            new("endpoint", endpoint) 
        };

        if (parameters != null)
        {
            foreach (var kvp in parameters)
                tags.Add(new KeyValuePair<string, object?>($"param_{kvp.Key}", kvp.Value));
        }

        _requestCounter.Add(1, tags.ToArray());

        if (durationMs.HasValue)
            _executionTime.Record(durationMs.Value, tags.ToArray());
    }
}