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
/// Поддерживает автоматическую нормализацию имён.
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
        if (string.IsNullOrWhiteSpace(meterName))
            throw new ArgumentException("Meter name cannot be null or empty", nameof(meterName));

        if (string.IsNullOrWhiteSpace(metricName))
            throw new ArgumentException("Metric name cannot be null or empty", nameof(metricName));

        var meter = _registry.GetOrCreate(meterName);
        var cleanName = NormalizeMetricName($"{meterName}.{metricName}");

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

        // Решаем, что записывать — счётчик или гистограмму
        if (value.HasValue)
        {
            var histKey = $"{cleanName}_hist";
            var histogram = _histograms.GetOrAdd(histKey, _ =>
                meter.CreateHistogram<double>(cleanName, unit: "units"));

            histogram.Record(value.Value, tags);
        }
        else
        {
            var countKey = $"{cleanName}_count";
            var counter = _counters.GetOrAdd(countKey, _ =>
                meter.CreateCounter<long>(cleanName, unit: "count"));

            counter.Add(1, tags);
        }
    }

    /// <summary>
    /// Удаляет лишние суффиксы и пробелы, заменяет небезопасные символы.
    /// </summary>
    private static string NormalizeMetricName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "unknown_metric";

        // Приводим к нижнему регистру, заменяем пробелы и многоточия
        var normalized = name
            .Trim()
            .Replace(" ", "_")
            .Replace("__", "_")
            .Replace("..", ".")
            .Replace("__", "_")
            .ToLowerInvariant();

        // Убираем служебные суффиксы, если они вдруг добавлены
        var endings = new[] { "_total", "_count", "_value", "_metric" };
        foreach (var suffix in endings)
        {
            if (normalized.EndsWith(suffix))
                normalized = normalized[..^suffix.Length];
        }

        return normalized;
    }

    /// <summary>
    /// Универсальный метод извлечения тегов из произвольного объекта.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> ExtractTags(object obj)
    {
        var tags = new List<KeyValuePair<string, string>>();

        switch (obj)
        {
            case IDictionary<string, object> dict:
                foreach (var (key, val) in dict)
                    tags.Add(new KeyValuePair<string, string>(key, val?.ToString() ?? string.Empty));
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
