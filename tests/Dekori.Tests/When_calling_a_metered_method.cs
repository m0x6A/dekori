using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_calling_a_metered_method : Specification
{
    private const int Calls = 3;
    private TestHost _host = null!;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<ICounterService, CounterService>());

    protected override Task When()
    {
        var service = _host.Resolve<ICounterService>();
        for (int i = 0; i < Calls; i++)
        {
            service.DoWork();
        }

        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_invocation_counter_is_incremented_per_call()
    {
        var calls = _host.Probe.Metrics.Where(m => m.Name == "demo.work.calls").ToList();
        calls.Count.ShouldBe(Calls);
        calls.Sum(m => m.Value).ShouldBe(Calls);
    }

    [Fact]
    public void Then_a_duration_is_recorded_per_call() =>
        _host.Probe.Metrics.Count(m => m.Name == "demo.work.duration").ShouldBe(Calls);

    [Fact]
    public void Then_metrics_carry_the_operation_tag() =>
        _host.Probe.Metrics
            .First(m => m.Name == "demo.work.calls")
            .Tags["code.function"].ShouldBe("CounterService.DoWork");
}
