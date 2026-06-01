using System.Text;
using Dekori;
using Dekori.Demo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Quieten the framework noise so the Dekori signals stand out on the console.
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

string? grafanaUrl        = builder.Configuration["Grafana:Url"];
string? grafanaInstanceId = builder.Configuration["Grafana:InstanceId"];
string? grafanaApiToken   = builder.Configuration["Grafana:ApiToken"];
bool    grafanaEnabled    = !string.IsNullOrEmpty(grafanaUrl)
                         && !string.IsNullOrEmpty(grafanaInstanceId)
                         && !string.IsNullOrEmpty(grafanaApiToken);

void ConfigureGrafana(OtlpExporterOptions otlp)
{
    otlp.Endpoint = new Uri(grafanaUrl!);
    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
    string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{grafanaInstanceId}:{grafanaApiToken}"));
    otlp.Headers = $"Authorization=Basic {credentials}";
}

// Wire OpenTelemetry to the "Dekori" ActivitySource and Meter.
// Console exporter always active; Grafana OTLP exporter active when secrets are present.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Dekori.Demo"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("Dekori").AddConsoleExporter();
        if (grafanaEnabled)
        {
            tracing.AddOtlpExporter(ConfigureGrafana);
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Dekori").AddConsoleExporter((_, reader) =>
            reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000);
        if (grafanaEnabled)
        {
            metrics.AddOtlpExporter(ConfigureGrafana);
        }
    });

if (grafanaEnabled)
{
    Console.WriteLine($"Grafana OTLP export enabled → {grafanaUrl}");
}

// Register Dekori and the instrumented services.
builder.Services.AddDekori();
builder.Services.AddInstrumented<IOrderService, OrderService>();
builder.Services.AddInstrumented<IRepository<Widget>, InMemoryRepository<Widget>>();

using var host = builder.Build();

// Start the host so OpenTelemetry instantiates its providers and subscribes to the "Dekori"
// ActivitySource/Meter — without this, nothing listens and no spans/metrics are exported.
await host.StartAsync();

var orders  = host.Services.GetRequiredService<IOrderService>();
var widgets = host.Services.GetRequiredService<IRepository<Widget>>();

Console.WriteLine("\n=== Dekori demo: instrumented calls ===\n");

// 1) Async traced + metered + logged call.
string orderId = await orders.PlaceOrderAsync("WIDGET-1", 3);
Console.WriteLine($"-> Placed {orderId}");

// 2) Generic instrumented class.
Widget widget = widgets.GetById(42);
Console.WriteLine($"-> Fetched {widget}");

// 3) Class-level instrumented (trace + metrics) method.
orders.Cancel("order-123");
Console.WriteLine("-> Cancelled order-123");

// 4) A failing call: the exception is recorded on the span and rethrown to us.
try
{
    await orders.PlaceOrderAsync("WIDGET-1", 0);
}
catch (ArgumentOutOfRangeException ex)
{
    Console.WriteLine($"-> Caught (and still instrumented): {ex.Message}");
}

Console.WriteLine("\n=== Flushing telemetry... ===\n");
await Task.Delay(1500); // let the periodic metric reader export at least once
await host.StopAsync(); // flushes remaining spans/metrics on shutdown
