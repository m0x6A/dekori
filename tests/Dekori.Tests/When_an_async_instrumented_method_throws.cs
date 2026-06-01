using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

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
