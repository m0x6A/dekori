namespace Dekori.Tests.Support;

/// <summary>
/// Minimal BDD base: <see cref="Given"/> arranges and <see cref="When"/> acts once before the
/// scenario's <c>[Fact]</c> "Then" assertions run. xUnit creates a fresh instance per test method,
/// so each assertion observes a clean Given/When.
/// </summary>
public abstract class Specification : IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        Given();
        await When();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Cleanup();
        return Task.CompletedTask;
    }

    /// <summary>Arrange the system under test. Runs before <see cref="When"/>.</summary>
    protected virtual void Given()
    {
    }

    /// <summary>Exercise the behavior under test.</summary>
    protected abstract Task When();

    /// <summary>Tear down resources created in <see cref="Given"/>.</summary>
    protected virtual void Cleanup()
    {
    }
}
