using System.Text;
using Dekori;
using Dekori.Demo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Quieten the framework noise so the Dekori signals stand out on the console.
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.Hosting", LogLevel.Warning);

string? grafanaUrl        = builder.Configuration["Grafana:Url"];
string? grafanaInstanceId = builder.Configuration["Grafana:InstanceId"];
string? grafanaApiToken   = builder.Configuration["Grafana:ApiToken"];
bool    grafanaEnabled    = !string.IsNullOrEmpty(grafanaUrl)
                         && !string.IsNullOrEmpty(grafanaInstanceId)
                         && !string.IsNullOrEmpty(grafanaApiToken);

void ConfigureGrafana(OtlpExporterOptions otlp, string signalPath)
{
    string baseUrl = grafanaUrl!.TrimEnd('/');
    otlp.Endpoint = new Uri($"{baseUrl}/{signalPath}");
    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
    string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{grafanaInstanceId}:{grafanaApiToken}"));
    otlp.Headers = $"Authorization=Basic {credentials}";
}

// Dashboard is created before Build so it can be registered as a logger provider, and before
// StartAsync so its ActivityListener/MeterListener are active before any calls are made.
using var dashboard = new DemoDashboard();
builder.Logging.AddProvider(dashboard);

// Wire OpenTelemetry to the "Dekori" ActivitySource, Meter and log pipeline.
// Grafana OTLP exporters are active when secrets are present; the DemoDashboard provides
// the in-process console summary instead of the verbose OTel console exporter.
var otelBuilder = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Dekori.Demo"))
    .WithTracing(tracing =>
    {
        tracing.AddSource("Dekori", "Dekori.Db");
        if (grafanaEnabled)
        {
            tracing.AddOtlpExporter(otlp => ConfigureGrafana(otlp, "v1/traces"));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Dekori");
        if (grafanaEnabled)
        {
            metrics.AddOtlpExporter(otlp => ConfigureGrafana(otlp, "v1/metrics"));
        }
    });

if (grafanaEnabled)
{
    otelBuilder.WithLogging(logging =>
        logging.AddOtlpExporter(otlp => ConfigureGrafana(otlp, "v1/logs")));
    Console.WriteLine($"Grafana OTLP export enabled → {grafanaUrl}");
}

// Register Dekori and the instrumented services. The extra "Dekori.Db" source is declared so it is
// pre-created and enumerable; the repository's [Trace(Source = "Dekori.Db")] emits its spans there.
builder.Services.AddDekori(options => options.AdditionalActivitySourceNames.Add("Dekori.Db"));
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

dashboard.Render();

Console.WriteLine("=== Flushing telemetry... ===\n");
await Task.Delay(1500); // allow Grafana OTLP export to drain before shutdown
await host.StopAsync(); // flushes remaining spans/metrics on shutdown
