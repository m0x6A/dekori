---
name: add-instrumentation
description: Scaffold a new Dekori instrumentation attribute/behavior (e.g. a new [Trace]-like or [Metric]-like attribute) across all required files. Use when adding a new attribute, a new telemetry signal, or a new per-method instrumentation behavior to the Dekori library under src/Dekori.
---

# Add a Dekori instrumentation behavior

Adding a new attribute-driven behavior to Dekori touches a **fixed set of files in a fixed order**.
Follow the touchpoints below so nothing is missed. Work **TDD-first**: write the failing spec
(use the `add-spec` skill) before wiring the interceptor logic.

Reuse existing primitives before adding new ones (YAGNI is a project value). The telemetry factory
already exposes `Source(name)`, `Counter(name)`, `Histogram(name, unit)` — do not create new
`ActivitySource`/`Meter`/instrument plumbing.

## Touchpoints (in order)

### 1. Attribute — `src/Dekori/{Name}Attribute.cs`
One public sealed attribute per file. Mirror `src/Dekori/MetricAttribute.cs`:

```csharp
namespace Dekori;

/// <summary>... XML docs are mandatory (public API + DocFX). ...</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class {Name}Attribute : Attribute
{
    /// <summary>...</summary>
    public string? Name { get; set; }
    // config properties with defaults
}
```
- Method/class-level behaviors target `Method | Class`. Parameter markers (like `[TraceParent]`,
  `[TraceLink]`) target `Parameter`.
- Auto-property defaults on the attribute are the *attribute* default; the *resolved* default
  (e.g. falling back to a `DekoriOptions` value) is applied in step 3.

### 2. Settings record — `src/Dekori/Instrumentation/{Name}Settings.cs`
An `internal sealed record` of the **resolved** runtime values only — no reflection, no attribute
reference. Mirror `src/Dekori/Instrumentation/MetricSettings.cs`:

```csharp
namespace Dekori.Instrumentation;

/// <summary>{Name} behavior settings resolved for a method.</summary>
internal sealed record {Name}Settings(string SomeValue, bool SomeFlag);
```

### 3. Resolution — `src/Dekori/Instrumentation/InstrumentationPlanCache.cs` (`Build`)
- Resolve the attribute with the existing helper:
  `{Name}Attribute? attr = Resolve<{Name}Attribute>(method, declaringType);`
  (`Resolve` returns the method-level attribute, else the class-level one.)
- Honor `classOptIn` (the `[Instrument]` class shorthand) if the behavior should be part of the
  default "instrument everything" set — see how `trace`/`metric`/`captureException` use `classOptIn`.
- Build the settings record, defaulting from `_options` where the attribute leaves a value unset
  (e.g. `string.IsNullOrWhiteSpace(attr?.Name) ? _options.SomeDefault : attr!.Name!`).
- Add the new settings to the early-out check and the `InstrumentationPlan` constructor call:
  ```csharp
  if (trace is null && metric is null && !captureException && log is null && {name} is null)
  {
      return InstrumentationPlan.None;
  }
  ...
  return new InstrumentationPlan(operationName, declaringType.Name, trace, metric, captureException, log, errorMetricBaseName, {name});
  ```

### 4. Plan — `src/Dekori/Instrumentation/InstrumentationPlan.cs`
- Add a ctor parameter and `public {Name}Settings? {Name} { get; }` property.
- Extend `IsInstrumented`: `... || Log is not null || {Name} is not null;`

### 5. Emission — `src/Dekori/DekoriInterceptor.cs` (`ExecuteAsync`)
The pipeline is: start span → `LogEntry` → `proceed()` → on success set status/record return/log exit
→ on exception capture/rethrow → `finally` records metrics, disposes span, restores `Activity.Current`.
Insert your behavior at the correct phase, guarded by a null check, and follow the existing rules:
```csharp
if (plan.{Name} is not null)
{
    // use _telemetry.Source/Counter/Histogram and GetLogger(plan.TypeName)
}
```
- **Hot path:** read everything from `plan` — never reflect here.
- **Async:** the method already awaits `proceed()` *inside* the span; keep durations covering the
  whole awaited operation.
- **Exceptions:** always rethrow (the `catch ... throw;` pattern). Set
  `ActivityStatusCode.Error` and use `AddException` for span error annotation.
- **Root spans:** preserve the `previousAmbient`/`Activity.Current = previousAmbient` restore in `finally`.
- **Tags:** use OTel semantic conventions already in use — `code.function`, `code.namespace`,
  `error.type`. Argument/return tags use the `dekori.arg.{name}` / `dekori.return` prefix.

### 6. Options — `src/Dekori/DekoriOptions.cs` (only if globally configurable)
Add a public property with a default and XML docs, mirroring `DefaultMetricName` / `DefaultLogLevel`.

## Definition of done
- Add/extend a BDD spec under `tests/Dekori.Tests/` (use the `add-spec` skill) asserting the real
  signal via `TelemetryProbe`.
- `dotnet build` is clean (**0 warnings** — CI runs `-warnaserror`).
- `dotnet test` is fully green.
- For an observable change, `dotnet run --project samples/Dekori.Demo` shows the new signal in the
  `DemoDashboard` tables (consider adding a demo call to `samples/Dekori.Demo/Program.cs`).
- Respect the conventions in `CLAUDE.md` (bracketed control flow, one type per file, file-scoped
  namespaces); run the `check-conventions` skill on the diff.
