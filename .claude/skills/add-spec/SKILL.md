---
name: add-spec
description: Author a Dekori BDD specification (xUnit + Shouldly + NSubstitute) following the Given/When/Then Specification + TelemetryProbe pattern. Use when adding or changing a test for the Dekori library, writing a TDD-first failing test, or verifying spans/metrics/logs behavior under tests/Dekori.Tests.
---

# Author a Dekori BDD specification

Tests are **TDD-first** and expressed as **behaviour specifications**. Assert **real in-process
signals** captured by `TelemetryProbe` — not mocks. Use only xUnit (runner), Shouldly (assertions),
and NSubstitute (test doubles for collaborators). One scenario (class) per file.

## The shape

- Class name reads as behaviour: `When_<scenario>` (sealed), deriving from `Support/Specification.cs`.
- Override `Given()` (arrange — build the `TestHost`), `When()` (act — exercise the SUT), and
  `Cleanup()` (`_host.Dispose()`).
- Each assertion is a `[Fact]` named `Then_<assertion_in_plain_english>` — one concern per fact.
- xUnit creates a fresh instance per `[Fact]`, so every assertion sees a clean Given/When.

```csharp
using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_<scenario> : Specification
{
    private TestHost _host = null!;
    private string _result = string.Empty;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IGreeter, Greeter>());

    protected override Task When()
    {
        _result = _host.Resolve<IGreeter>().Greet("Ada");
        return Task.CompletedTask; // or `async` + await for Task-returning SUTs
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_real_result_is_returned() => _result.ShouldBe("Hello Ada");

    [Fact]
    public void Then_exactly_one_span_is_produced() => _host.Probe.Activities.Count.ShouldBe(1);
}
```

## TestHost API (`tests/Dekori.Tests/Support/TestHost.cs`)
Each host gets unique source/meter names, so parallel tests never see each other's signals.
```csharp
new TestHost(
    services => services.AddInstrumented<IFoo, Foo>(),   // required: register the instrumented service
    configureOptions: opts => opts.CaptureArgumentsByDefault = true, // optional
    additionalSources: ["Dekori.Db"]);                   // optional: named [Trace(Source=...)] sources
```
- `_host.Resolve<IFoo>()` — resolve the proxied service.
- `_host.Probe` — the `TelemetryProbe` (spans + metrics).
- `_host.Logs` — `IReadOnlyList<LogEntry>` (Level/Category/Message/Exception).

## Asserting real signals

**Spans** — `_host.Probe.Activities` (`System.Diagnostics.Activity`):
```csharp
var span = _host.Probe.Activities.Single();
span.DisplayName.ShouldBe("Greeter.Greet");
span.Status.ShouldBe(ActivityStatusCode.Ok);          // or .Error
span.Source.Name.ShouldBe(_host.SourceName);
span.Tag("code.function").ShouldBe("Greeter.Greet");  // Support/ActivityExtensions.Tag(...)
span.Tag("dekori.arg.name").ShouldBe("Ada");          // arg/return tags
span.Events.ShouldContain(e => e.Name == "exception");
span.Links.Count().ShouldBe(1);
span.Duration.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(5);
```

**Metrics** — `_host.Probe.Metrics` (`RecordedMetric(string Name, double Value, IReadOnlyDictionary<string, object?> Tags)`):
```csharp
var calls = _host.Probe.Metrics.Where(m => m.Name == "demo.work.calls").ToList();
calls.Count.ShouldBe(3);
calls.Sum(m => m.Value).ShouldBe(3);
calls[0].Tags["code.function"].ShouldBe("CounterService.DoWork");
```
Instrument names follow `{base}.calls`, `{base}.duration`, `{base}.errors`.

**Logs** — `_host.Logs`:
```csharp
_host.Logs.ShouldContain(l => l.Level == LogLevel.Error && l.Exception is InvalidOperationException);
```

## Test doubles
- Add a new instrumented surface (interface + impl, e.g. `IFoo`/`Foo`) under `Support/` when the
  scenario needs one. Decorate it with the Dekori attribute under test.
- Use **NSubstitute** for *collaborators* the SUT depends on: `Substitute.For<IInventory>()`,
  `.Returns(...)`, `.Received(...)`. See `When_the_proxy_wraps_a_service_with_a_substituted_collaborator.cs`.

## Testing failure paths
Catch the exception in `When()` into a field and assert on it plus the error signals:
```csharp
protected override Task When()
{
    try { _host.Resolve<IFragile>().Break(); }
    catch (Exception ex) { _caught = ex; }
    return Task.CompletedTask;
}
```

## Reference specs
`When_calling_a_traced_method.cs`, `When_an_instrumented_method_throws.cs`,
`When_calling_a_metered_method.cs`, `When_a_traced_method_links_to_other_traces.cs`.

## Done
`dotnet test` green; `dotnet build` clean (0 warnings). Run `check-conventions` on the new file.
