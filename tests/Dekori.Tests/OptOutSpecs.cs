using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_a_class_is_instrumented_with_an_opt_out_method : Specification
{
    private TestHost _host = null!;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IMixed, Mixed>());

    protected override Task When()
    {
        var service = _host.Resolve<IMixed>();
        service.Tracked();
        service.Untracked();
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_only_the_non_opted_out_method_produces_a_span()
    {
        _host.Probe.Activities.Count.ShouldBe(1);
        _host.Probe.Activities.Single().DisplayName.ShouldBe("Mixed.Tracked");
    }
}
