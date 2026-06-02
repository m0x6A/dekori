# Configuration

All options are set via the `AddDekori` callback:

```csharp
builder.Services.AddDekori(options =>
{
    options.ActivitySourceName           = "MyApp";
    options.MeterName                    = "MyApp";
    options.Version                      = "1.0";
    options.DefaultMetricName            = "myapp.method";
    options.DefaultLogLevel              = LogLevel.Debug;
    options.CaptureArgumentsByDefault    = false;
    options.CaptureReturnValueByDefault  = false;
    options.AdditionalActivitySourceNames.Add("MyApp.Database");
});
```

## Options reference

| Option | Default | Description |
|--------|---------|-------------|
| `ActivitySourceName` | `"Dekori"` | Name passed to `AddSource(...)` in OpenTelemetry tracing setup. |
| `MeterName` | `"Dekori"` | Name passed to `AddMeter(...)` in OpenTelemetry metrics setup. |
| `Version` | `null` | Version reported for the `ActivitySource` and `Meter`. |
| `DefaultMetricName` | `"dekori.method"` | Base name for metric instruments when `[Metric]` has no explicit `Name`. Produces `{name}.calls`, `{name}.duration`, and `{name}.errors`. |
| `DefaultLogLevel` | `Debug` | Log level used when `[LogCall]` has no explicit `Level`. |
| `CaptureArgumentsByDefault` | `false` | When true, every `[Trace]` records arguments as span tags unless the attribute sets `RecordArguments = false`. |
| `CaptureReturnValueByDefault` | `false` | When true, every `[Trace]` records the return value as a span tag unless the attribute sets `RecordReturnValue = false`. |
| `AdditionalActivitySourceNames` | `[]` | Extra `ActivitySource` names pre-created at startup for methods that use `[Trace(Source = "...")]`. Pre-registering ensures the source appears in `DekoriTelemetry.SourceNames` before the first call. |

## Multiple activity sources

Splitting spans across named sources lets consumers subscribe only to the layer they care about:

```csharp
builder.Services.AddDekori(options =>
{
    options.ActivitySourceName = "MyApp";
    options.AdditionalActivitySourceNames.Add("MyApp.Database");
});

// Tracing subscription:
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource("MyApp")
        .AddSource("MyApp.Database")
        .AddConsoleExporter());
```

On the service, emit individual methods from the database source:

```csharp
[Trace(Source = "MyApp.Database")]
public Widget QueryDatabase(int id) { ... }
```
