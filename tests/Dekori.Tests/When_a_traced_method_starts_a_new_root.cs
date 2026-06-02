using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_a_traced_method_starts_a_new_root : Specification
{
    private TestHost _host = null!;
    private ActivityTraceId _ambientTraceId;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<ITraceComposer, TraceComposer>());

    protected override Task When()
    {
        using var ambientSource = new ActivitySource("Dekori.Test.Ambient");
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Dekori.Test.Ambient",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity ambient = ambientSource.StartActivity("ambient")!;
        _ambientTraceId = ambient.TraceId;
        _host.Resolve<ITraceComposer>().StartFresh();
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_exactly_one_span_is_produced() => _host.Probe.Activities.Count.ShouldBe(1);

    [Fact]
    public void Then_the_span_has_no_parent() => _host.Probe.Activities.Single().Parent.ShouldBeNull();

    [Fact]
    public void Then_the_span_starts_its_own_trace() =>
        _host.Probe.Activities.Single().TraceId.ShouldNotBe(_ambientTraceId);
}
