using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_a_traced_method_specifies_an_explicit_parent : Specification
{
    private TestHost _host = null!;
    private ActivityContext _parent;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<ITraceComposer, TraceComposer>());

    protected override Task When()
    {
        _parent = new ActivityContext(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        _host.Resolve<ITraceComposer>().ContinueFrom(_parent);
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_span_joins_the_parent_trace() =>
        _host.Probe.Activities.Single().TraceId.ShouldBe(_parent.TraceId);

    [Fact]
    public void Then_the_span_is_a_child_of_the_given_parent() =>
        _host.Probe.Activities.Single().ParentSpanId.ShouldBe(_parent.SpanId);
}
