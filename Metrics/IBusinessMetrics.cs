using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace MetricsDemo.Metrics;

public interface IBusinessMetrics
{
    /// <summary>
    /// Универсальный метод для записи бизнесовых метрик.
    /// Поддерживает любое количество произвольных аргументов и динамические домены.
    /// </summary>
    void Track(string meterName, string metricName, params object[] args);
}

/// <summary>
/// Основной сервис для трекинга бизнесовых метрик.
/// Автоматически создает счетчики и гистограммы для разных доменов.
/// </summary>
public class BusinessMetricsService : IBusinessMetrics
{
    private readonly IMeterRegistry _registry;
    private readonly ConcurrentDictionary<string, Counter<long>> _counters = new();
    private readonly ConcurrentDictionary<string, Histogram<double>> _histograms = new();

    public BusinessMetricsService(IMeterRegistry registry)
    {
        _registry = registry;
    }

    public void Track(string meterName, string metricName, params object[] args)
    {
        var meter = _registry.GetOrCreate(meterName);

        double? value = null;
        var tags = new TagList();

        // Извлекаем значение и метки
        foreach (var arg in args)
        {
            if (arg == null) continue;

            switch (arg)
            {
                case double d: value = d; continue;
                case float f: value = f; continue;
                case int i: value = i; continue;
                case long l: value = l; continue;
                case decimal dec: value = (double)dec; continue;
            }

            foreach (var kv in ExtractTags(arg))
                tags.Add(kv.Key, kv.Value);
        }

        // Решаем, какой инструмент использовать
        if (value.HasValue)
        {
            var histKey = $"{meterName}.{metricName}";
            if (!_histograms.TryGetValue(histKey, out var histogram))
            {
                histogram = meter.CreateHistogram<double>($"{meterName}_{metricName}_value", unit: "units");
                _histograms[histKey] = histogram;
            }

            histogram.Record(value.Value, tags);
        }
        else
        {
            var countKey = $"{meterName}.{metricName}";
            if (!_counters.TryGetValue(countKey, out var counter))
            {
                counter = meter.CreateCounter<long>($"{meterName}_{metricName}_total", unit: "count");
                _counters[countKey] = counter;
            }

            counter.Add(1, tags);
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractTags(object obj)
    {
        var tags = new List<KeyValuePair<string, string>>();

        switch (obj)
        {
            case IDictionary<string, object> dict:
                tags.AddRange(dict.Select(kv => new KeyValuePair<string, string>(
                    kv.Key,
                    kv.Value?.ToString() ?? string.Empty
                )));
                break;

            default:
                var type = obj.GetType();
                if (!type.IsPrimitive && type != typeof(string))
                {
                    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var val = prop.GetValue(obj);
                        if (val != null)
                            tags.Add(new KeyValuePair<string, string>(prop.Name, val.ToString()!));
                    }
                }
                break;
        }

        return tags;
    }
}
