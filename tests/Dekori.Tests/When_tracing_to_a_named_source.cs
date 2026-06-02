using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_tracing_to_a_named_source : Specification
{
    private TestHost _host = null!;
    private string _result = string.Empty;

    protected override void Given() =>
        _host = new TestHost(
            services => services.AddInstrumented<ITraceComposer, TraceComposer>(),
            additionalSources: [TraceComposer.NamedSource]);

    protected override Task When()
    {
        _result = _host.Resolve<ITraceComposer>().MapTo("Bornholm");
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_real_result_is_returned() => _result.ShouldBe("Mapped Bornholm");

    [Fact]
    public void Then_exactly_one_span_is_produced() => _host.Probe.Activities.Count.ShouldBe(1);

    [Fact]
    public void Then_the_span_is_emitted_from_the_named_source() =>
        _host.Probe.Activities.Single().Source.Name.ShouldBe(TraceComposer.NamedSource);
}
