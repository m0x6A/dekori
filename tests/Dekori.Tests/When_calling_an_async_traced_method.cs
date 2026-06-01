using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_calling_an_async_traced_method : Specification
{
    private const int DelayMs = 80;
    private TestHost _host = null!;
    private string _result = string.Empty;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IGreeter, Greeter>());

    protected override async Task When() =>
        _result = await _host.Resolve<IGreeter>().GreetAsync("Bob", DelayMs);

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_awaited_result_is_returned() => _result.ShouldBe("Hello Bob");

    [Fact]
    public void Then_the_custom_span_name_is_used() =>
        _host.Probe.Activities.Single().DisplayName.ShouldBe("greet.async");

    [Fact]
    public void Then_the_span_duration_covers_the_whole_async_operation() =>
        // The strongest proof that the span wraps the awaited work, not just the sync prologue.
        _host.Probe.Activities.Single().Duration.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(DelayMs - 20);

    [Fact]
    public void Then_arguments_are_recorded_when_opted_in() =>
        _host.Probe.Activities.Single().Tag("dekori.arg.name").ShouldBe("Bob");

    [Fact]
    public void Then_the_return_value_is_recorded_when_opted_in() =>
        _host.Probe.Activities.Single().Tag("dekori.return").ShouldBe("Hello Bob");
}
