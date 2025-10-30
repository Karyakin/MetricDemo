using MetricsDemo.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace MetricsDemo.Controllers;

[ApiController]
[Route("test")]
public class WeatherController : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild",
        "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet("forecast")]
    [UseMetrics]
    public IEnumerable<object> GetForecast()
    {
        var rng = new Random();
        return Enumerable.Range(1, 5).Select(index => new
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = rng.Next(-20, 35),
            Summary = Summaries[rng.Next(Summaries.Length)]
        });
    }

    [HttpGet]
    [UseMetrics("cityName")]
    public IActionResult GetByCity([FromQuery] City city)
    {
        var rng = new Random();
        var result = new
        {
            City = city,
            TemperatureC = rng.Next(-10, 35),
            Summary = Summaries[rng.Next(Summaries.Length)]
        };
        return Ok(result);
    }

    [HttpPost("temperature")]
    [UseMetrics("min", "max", "cityName", "IsSuccess")]
    public IActionResult FilterByTemperature([FromBody] MyClass myClass)
    {
        var rng = new Random();
        var data = Enumerable.Range(1, 10)
            .Select(_ => rng.Next(-10, 40))
            .Where(t => t >= myClass.Min && t <= myClass.Max);

        return Ok(data);
    }

    public class MyClass
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public City City { get; set; } = new();
    }

    public class City
    {
        public string CityName { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
    }

}