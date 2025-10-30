using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace MetricsDemo.Metrics;

using System.Diagnostics.Metrics;
using System.Reflection;


public interface IBusinessMetrics
{
    /// <summary>
    /// Универсальный метод для записи бизнесовых метрик.
    /// Может принимать любое количество произвольных аргументов.
    /// </summary>
    void Track(string name, params object[] args);
}


public class BusinessMetricsService : IBusinessMetrics
{
    private readonly Meter _meter;
    private readonly Dictionary<string, Counter<long>> _counters = new();
    private readonly Dictionary<string, Histogram<double>> _histograms = new();

    public BusinessMetricsService(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("BusinessMetrics", "4.0.0");
    }

    public void Track(string name, params object[] args)
    {
        double? value = null;
        var tags = new TagList();

        // извлекаем value (если оно есть) и метки
        foreach (var arg in args)
        {
            if (arg == null) continue;

            if (arg is double d) { value = d; continue; }
            if (arg is float f) { value = f; continue; }
            if (arg is int i) { value = i; continue; }
            if (arg is long l) { value = l; continue; }
            if (arg is decimal dec) { value = (double)dec; continue; }

            // преобразуем в теги
            foreach (var kv in ExtractTags(arg))
                tags.Add(kv.Key, kv.Value);
        }

        if (value.HasValue)
        {
            // Histogram — если есть числовое значение
            if (!_histograms.TryGetValue(name, out var histogram))
            {
                histogram = _meter.CreateHistogram<double>($"business_{name}_value", unit: "units");
                _histograms[name] = histogram;
            }

            histogram.Record(value.Value, tags);
        }
        else
        {
            // Counter — если нет числового значения
            if (!_counters.TryGetValue(name, out var counter))
            {
                counter = _meter.CreateCounter<long>($"business_{name}_total", unit: "count");
                _counters[name] = counter;
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
                tags.AddRange(dict.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value?.ToString() ?? "")));
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
