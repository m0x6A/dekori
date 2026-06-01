using Dekori.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Dekori.Tests;

public sealed class When_logging_is_enabled_for_a_method : Specification
{
    private TestHost _host = null!;
    private int _result;

    protected override void Given() =>
        _host = new TestHost(services => services.AddInstrumented<IChatter, Chatter>());

    protected override Task When()
    {
        _result = _host.Resolve<IChatter>().Speak("hello");
        return Task.CompletedTask;
    }

    protected override void Cleanup() => _host.Dispose();

    [Fact]
    public void Then_the_real_result_is_returned() => _result.ShouldBe(5);

    [Fact]
    public void Then_an_entry_log_is_written_at_the_configured_level() =>
        _host.Logs.ShouldContain(l =>
            l.Level == LogLevel.Information && l.Message.Contains("→") && l.Message.Contains("Chatter.Speak"));

    [Fact]
    public void Then_an_exit_log_is_written() =>
        _host.Logs.ShouldContain(l => l.Level == LogLevel.Information && l.Message.Contains("←"));

    [Fact]
    public void Then_arguments_are_logged_when_opted_in() =>
        _host.Logs.ShouldContain(l => l.Message.Contains("hello"));

    [Fact]
    public void Then_the_result_is_logged_when_opted_in() =>
        _host.Logs.ShouldContain(l => l.Message.Contains("←") && l.Message.Contains("5"));
}
