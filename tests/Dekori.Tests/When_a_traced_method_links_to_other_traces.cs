using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_a_traced_method_links_to_other_traces : Specification
{
    private TestHost _host = null!;
    private ActivityContext _link;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<ITraceComposer, TraceComposer>());

    protected override Task When()
    {
        _link = new ActivityContext(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        _host.Resolve<ITraceComposer>().RelateTo(_link);
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_span_carries_exactly_one_link() =>
        _host.Probe.Activities.Single().Links.Count().ShouldBe(1);

    [Fact]
    public void Then_the_link_points_at_the_supplied_context() =>
        _host.Probe.Activities.Single().Links.Single().Context.ShouldBe(_link);
}
