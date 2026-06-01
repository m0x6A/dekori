using System.Diagnostics;
using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_calling_a_traced_method : Specification
{
    private TestHost _host = null!;
    private string _result = string.Empty;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IGreeter, Greeter>());

    protected override Task When()
    {
        _result = _host.Resolve<IGreeter>().Greet("Ada");
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_real_result_is_returned() => _result.ShouldBe("Hello Ada");

    [Fact]
    public void Then_exactly_one_span_is_produced() => _host.Probe.Activities.Count.ShouldBe(1);

    [Fact]
    public void Then_the_span_is_named_after_the_operation() =>
        _host.Probe.Activities.Single().DisplayName.ShouldBe("Greeter.Greet");

    [Fact]
    public void Then_the_span_carries_the_code_function_tag() =>
        _host.Probe.Activities.Single().Tag("code.function").ShouldBe("Greeter.Greet");

    [Fact]
    public void Then_the_span_completes_with_ok_status() =>
        _host.Probe.Activities.Single().Status.ShouldBe(ActivityStatusCode.Ok);
}
