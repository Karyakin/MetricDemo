using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MetricsDemo.Metrics;

public interface IMeterRegistry
{
    Meter GetOrCreate(string meterName);
    IEnumerable<string> GetAllMeterNames();
    void OnNewMeter(Action<string> callback);
}

public class MeterRegistry : IMeterRegistry
{
    private readonly IMeterFactory _factory;
    private readonly ConcurrentDictionary<string, Meter> _meters = new();
    private readonly List<Action<string>> _onNewMeterCallbacks = new();

    public MeterRegistry(IMeterFactory factory)
    {
        _factory = factory;
    }

    public Meter GetOrCreate(string meterName)
    {
        return _meters.GetOrAdd(meterName, name =>
        {
            var meter = _factory.Create(name, "1.0.0");
            foreach (var cb in _onNewMeterCallbacks)
                cb(name); // уведомляем слушателей
            return meter;
        });
    }

    public IEnumerable<string> GetAllMeterNames() => _meters.Keys;

    public void OnNewMeter(Action<string> callback)
    {
        _onNewMeterCallbacks.Add(callback);
    }
}