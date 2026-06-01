using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_an_instrumented_method_throws : Specification
{
    private TestHost _host = null!;
    private Exception? _caught;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IFragile, Fragile>());

    protected override Task When()
    {
        try
        {
            _host.Resolve<IFragile>().Break();
        }
        catch (Exception ex)
        {
            _caught = ex;
        }

        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_exception_is_rethrown_to_the_caller() =>
        _caught.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");

    [Fact]
    public void Then_the_span_status_is_error() =>
        _host.Probe.Activities.Single().Status.ShouldBe(ActivityStatusCode.Error);

    [Fact]
    public void Then_the_exception_is_recorded_on_the_span() =>
        _host.Probe.Activities.Single().Events.ShouldContain(e => e.Name == "exception");

    [Fact]
    public void Then_the_error_counter_is_incremented()
    {
        var errors = _host.Probe.Metrics.Where(m => m.Name == "demo.fragile.errors").ToList();
        errors.Count.ShouldBe(1);
        errors[0].Tags["error.type"].ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void Then_the_failure_is_logged_at_error_level() =>
        _host.Logs.ShouldContain(l => l.Level == LogLevel.Error && l.Exception is InvalidOperationException);
}

public sealed class When_an_async_instrumented_method_throws : Specification
{
    private TestHost _host = null!;
    private Exception? _caught;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IFragile, Fragile>());

    protected override async Task When()
    {
        try
        {
            await _host.Resolve<IFragile>().BreakAsync();
        }
        catch (Exception ex)
        {
            _caught = ex;
        }
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_exception_propagates_through_the_task() =>
        _caught.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("async boom");

    [Fact]
    public void Then_the_span_status_is_error() =>
        _host.Probe.Activities.Single().Status.ShouldBe(ActivityStatusCode.Error);

    [Fact]
    public void Then_the_exception_is_recorded_on_the_span() =>
        _host.Probe.Activities.Single().Events.ShouldContain(e => e.Name == "exception");
}
