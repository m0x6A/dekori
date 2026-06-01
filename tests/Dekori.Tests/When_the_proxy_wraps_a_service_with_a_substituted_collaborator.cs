using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_the_proxy_wraps_a_service_with_a_substituted_collaborator : Specification
{
    private readonly IInventory _inventory = Substitute.For<IInventory>();
    private TestHost _host = null!;
    private bool _result;

    protected override void Given()
    {
        _inventory.Reserve("sku-1", 2).Returns(true);
        _host = new TestHost(services =>
        {
            services.AddSingleton(_inventory);
            services.AddInstrumented<IOrderService, OrderService>();
        });
    }

    protected override Task When()
    {
        _result = _host.Resolve<IOrderService>().Place("sku-1", 2);
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_real_method_runs_and_returns_the_collaborator_result() => _result.ShouldBeTrue();

    [Fact]
    public void Then_the_collaborator_received_the_forwarded_call() =>
        _inventory.Received(1).Reserve("sku-1", 2);

    [Fact]
    public void Then_the_call_is_still_traced() =>
        _host.Probe.Activities.Single().DisplayName.ShouldBe("OrderService.Place");
}
