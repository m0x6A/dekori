using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

/// <summary>
/// Documents the per-call overhead introduced by the DynamicProxy + OpenTelemetry pipeline.
/// A TelemetryProbe is active so <see cref="ActivitySource.StartActivity"/> creates real spans.
/// </summary>
public sealed class When_measuring_instrumentation_overhead : Specification
{
    private const int WarmupIterations = 1_000;
    private const int MeasuredIterations = 10_000;

    private IGreeter _direct = null!;
    private IGreeter _instrumented = null!;
    private TestHost _host = null!;
    private TimeSpan _directElapsed;
    private TimeSpan _instrumentedElapsed;

    protected override void Given()
    {
        _direct = new Greeter();
        _host = new TestHost(services => services.AddInstrumented<IGreeter, Greeter>());
        _instrumented = _host.Resolve<IGreeter>();
    }

    protected override Task When()
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            _direct.Greet("warmup");
            _instrumented.Greet("warmup");
        }

        long directStart = Stopwatch.GetTimestamp();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            _direct.Greet("bench");
        }
        _directElapsed = Stopwatch.GetElapsedTime(directStart);

        long instrumentedStart = Stopwatch.GetTimestamp();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            _instrumented.Greet("bench");
        }
        _instrumentedElapsed = Stopwatch.GetElapsedTime(instrumentedStart);

        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_instrumented_calls_are_slower_than_direct_calls() =>
        _instrumentedElapsed.ShouldBeGreaterThan(_directElapsed);

    [Fact]
    public void Then_per_call_overhead_is_below_500_microseconds()
    {
        double overheadUs = (_instrumentedElapsed - _directElapsed).TotalMicroseconds / MeasuredIterations;
        overheadUs.ShouldBeLessThan(500.0);
    }
}
