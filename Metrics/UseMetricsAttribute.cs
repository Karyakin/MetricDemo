using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;
using System.Reflection;

namespace MetricsDemo.Metrics;

[AttributeUsage(AttributeTargets.Method)]
public class UseMetricsAttribute : ActionFilterAttribute
{
    private readonly string[] _trackedParameters;

    public UseMetricsAttribute(params string[] trackedParameters)
    {
        _trackedParameters = trackedParameters;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Items["__metrics_timer"] = Stopwatch.StartNew();

        // Собираем все параметры, включая вложенные
        var trackedValues = new Dictionary<string, object?>();

        foreach (var (argName, argValue) in context.ActionArguments)
        {
            if (argValue == null) continue;
            CollectValues(argValue, prefix: argName, trackedValues);
        }

        // Фильтруем только нужные параметры, если указаны
        if (_trackedParameters.Length > 0)
        {
            trackedValues = trackedValues
                .Where(kvp => _trackedParameters
                    .Any(p => string.Equals(p, kvp.Key, StringComparison.OrdinalIgnoreCase)
                           || kvp.Key.EndsWith("." + p, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        context.HttpContext.Items["__metrics_params"] = trackedValues;
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        var stopwatch = context.HttpContext.Items["__metrics_timer"] as Stopwatch;
        stopwatch?.Stop();

        var metrics = context.HttpContext.RequestServices.GetRequiredService<MetricsService>();

        var endpoint = $"{context.ActionDescriptor.RouteValues["controller"]}/{context.ActionDescriptor.RouteValues["action"]}";
        var parameters = context.HttpContext.Items["__metrics_params"] as Dictionary<string, object?> ?? new();

        metrics.RecordRequest(endpoint, parameters!, stopwatch?.Elapsed.TotalMilliseconds ?? 0);
    }

    /// <summary>
    /// Рекурсивно собирает все публичные свойства объекта (вложенные тоже)
    /// </summary>
    private void CollectValues(object obj, string prefix, Dictionary<string, object?> output)
    {
        var type = obj.GetType();

        // Если это примитив или строка — добавляем напрямую
        if (type.IsPrimitive || obj is string || obj is DateTime || obj is Guid)
        {
            output[prefix] = obj;
            return;
        }

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(obj);
            if (value == null) continue;

            var key = $"{prefix}.{prop.Name}";
            if (value.GetType().IsPrimitive || value is string || value is DateTime || value is Guid)
            {
                output[key] = value;
            }
            else
            {
                CollectValues(value, key, output); // рекурсивно спускаемся внутрь
            }
        }
    }
}
