using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dekori.Tests.Support;

/// <summary>
/// Builds an isolated DI container with Dekori configured against unique source/meter names (so
/// parallel tests never observe each other's signals) plus a <see cref="TelemetryProbe"/> and a
/// recording logger.
/// </summary>
public sealed class TestHost : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly RecordingLoggerProvider _loggerProvider = new();

    public TestHost(Action<IServiceCollection> configure, Action<DekoriOptions>? configureOptions = null)
    {
        string id = Guid.NewGuid().ToString("N");
        SourceName = $"Dekori.Test.{id}";
        MeterName = $"Dekori.Test.{id}";

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(_loggerProvider);
        });
        services.AddDekori(options =>
        {
            options.ActivitySourceName = SourceName;
            options.MeterName = MeterName;
            configureOptions?.Invoke(options);
        });
        configure(services);

        _provider = services.BuildServiceProvider();
        Probe = new TelemetryProbe(SourceName, MeterName);
    }

    public string SourceName { get; }

    public string MeterName { get; }

    public TelemetryProbe Probe { get; }

    public IReadOnlyList<LogEntry> Logs => _loggerProvider.Entries;

    public T Resolve<T>() where T : notnull => _provider.GetRequiredService<T>();

    public void Dispose()
    {
        Probe.Dispose();
        _provider.Dispose();
        _loggerProvider.Dispose();
    }
}
