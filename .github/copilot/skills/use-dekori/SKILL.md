---
name: use-dekori
description: Use Dekori from a consumer .NET application installed via NuGet. Focus on registration, attributes, and OpenTelemetry wiring rather than Dekori's repository internals.
applyTo:
  - "**/*.cs"
  - "**/*.csproj"
---

# Use Dekori

Apply this skill when a .NET application uses **Dekori as a NuGet package**.

Assume the consuming app does **not** have Dekori source code, tests, or demo files checked in.

## Integration checklist

1. Add the package: `dotnet add package Dekori`
2. Register Dekori once during startup with `services.AddDekori()`
3. Register instrumented services with `services.AddInstrumented<TInterface, TImplementation>()` when possible
4. Decorate service classes or methods with Dekori attributes such as `[Instrument]`, `[Trace]`, `[Metric]`, `[CaptureException]`, and `[LogCall]`
5. Wire OpenTelemetry to the same source and meter names configured in `DekoriOptions`

## Service registration guidance

- Prefer `AddInstrumented<TInterface, TImplementation>()` so all interface members are interceptable.
- Use `AddInstrumented<T>()` only when the caller must resolve the concrete type; only `virtual` and `abstract` members are intercepted in that mode.
- Resolve the instrumented service from dependency injection. Constructing the implementation manually bypasses Dekori.
- Call `AddDekori(options => ...)` to customize names such as `ActivitySourceName`, `MeterName`, `DefaultMetricName`, and `DefaultLogLevel`.

## Attribute guidance

- `[Instrument]` is the class-level convenience option for trace + metric + exception capture on each method.
- `[Trace]` starts an `Activity`; use it when naming, parenting, linking, or root-span behavior needs to be explicit.
- `[Metric]` records invocation count and duration; the error counter is used with `[CaptureException]`.
- `[CaptureException]` logs, annotates telemetry, increments the error counter, and rethrows.
- `[LogCall]` adds entry/exit logs and supports optional argument/result logging.
- `[NoInstrument]` opts a single method out of class-level instrumentation.
- Keep argument and return-value capture disabled unless the values are safe to emit.

## OpenTelemetry wiring reminders

- Subscribe tracing to Dekori's configured activity source name with `AddSource(...)`.
- Subscribe metrics to Dekori's configured meter name with `AddMeter(...)`.
- If `[Trace(Source = "...")]` uses extra source names, add them to `DekoriOptions.AdditionalActivitySourceNames` and to OpenTelemetry tracing configuration.
- Logging flows through the normal `ILogger` pipeline; configure logging exporters separately if needed.

## Consumer-focused assumptions

- Do not reference Dekori repository paths like `src/Dekori/` or `samples/Dekori.Demo/` in suggestions.
- Do not ask the user to edit Dekori internals when the task is about using the package.
- Prefer showing how to configure the consumer's host, DI registrations, service interfaces, and attributes.
