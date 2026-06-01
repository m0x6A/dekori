using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_calling_a_generic_instrumented_class : Specification
{
    private TestHost _host = null!;
    private string _result = string.Empty;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IRepository<string>, Repository<string>>());

    protected override Task When()
    {
        _result = _host.Resolve<IRepository<string>>().Echo("payload");
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_value_round_trips() => _result.ShouldBe("payload");

    [Fact]
    public void Then_a_span_is_produced_for_the_generic_method() =>
        _host.Probe.Activities.Single().DisplayName.ShouldEndWith(".Echo");
}
