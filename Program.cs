using MetricsDemo.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// Контроллеры и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Наши сервисы
builder.Services.AddSingleton<IMeterRegistry, MeterRegistry>();
builder.Services.AddSingleton<IBusinessMetrics, BusinessMetricsService>();
builder.Services.AddSingleton<MetricsService>();

// Конфигурация OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(mb =>
    {
        mb
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MetricsDemoApp"))
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            // ⬇️ Просто добавляем имена всех метрик, которые могут быть в приложении
            .AddMeter("payment", "purchase", "user")
            .AddPrometheusExporter();
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();
