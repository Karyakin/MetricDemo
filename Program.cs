using MetricsDemo.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Подключаем контроллеры
builder.Services.AddControllers();

// 🔹 Swagger (чтобы работал UI)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔹 Подключаем сервис метрик
builder.Services.AddSingleton<MetricsService>();

// 🔹 Конфигурация OpenTelemetry + Prometheus
builder.Services.AddOpenTelemetry()
    .WithMetrics(mb =>
    {
        mb.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MetricsDemoApp"))
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter("MetricsDemoApp")
            .AddPrometheusExporter();
    });

var app = builder.Build();

// 🔹 Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 🔹 Маршрутизация и метрики
app.UseRouting();
app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();