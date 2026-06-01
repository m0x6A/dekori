using Castle.DynamicProxy;
using Dekori.Instrumentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dekori;

/// <summary>
/// Registration helpers that wire Dekori into <see cref="IServiceCollection"/> and produce
/// instrumented proxies for your services.
/// </summary>
public static class DekoriServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dekori infrastructure (options, telemetry, proxy generator and interceptor).
    /// Call once, then register services with <see cref="AddInstrumented{TInterface, TImplementation}"/>.
    /// </summary>
    public static IServiceCollection AddDekori(this IServiceCollection services, Action<DekoriOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new DekoriOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<DekoriTelemetry>();
        services.TryAddSingleton<InstrumentationPlanCache>();
        services.TryAddSingleton(new ProxyGenerator());
        services.TryAddSingleton(sp => new DekoriInterceptor(
            sp.GetRequiredService<InstrumentationPlanCache>(),
            sp.GetRequiredService<DekoriTelemetry>(),
            sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> and exposes <typeparamref name="TInterface"/>
    /// as an instrumented interface proxy that wraps it. This is the recommended registration shape.
    /// </summary>
    public static IServiceCollection AddInstrumented<TInterface, TImplementation>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(TImplementation), typeof(TImplementation), lifetime));
        services.Add(new ServiceDescriptor(typeof(TInterface), sp =>
        {
            var generator = sp.GetRequiredService<ProxyGenerator>();
            var interceptor = sp.GetRequiredService<DekoriInterceptor>();
            var target = sp.GetRequiredService<TImplementation>();
            return generator.CreateInterfaceProxyWithTargetInterface(
                typeof(TInterface), target, interceptor.ToInterceptor());
        }, lifetime));

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="T"/> as an instrumented class proxy. Only <see langword="virtual"/>
    /// members can be intercepted — prefer the interface overload where possible.
    /// </summary>
    public static IServiceCollection AddInstrumented<T>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(T), sp =>
        {
            var generator = sp.GetRequiredService<ProxyGenerator>();
            var interceptor = sp.GetRequiredService<DekoriInterceptor>();
            var target = ActivatorUtilities.CreateInstance<T>(sp);
            return generator.CreateClassProxyWithTarget(target, interceptor.ToInterceptor());
        }, lifetime));

        return services;
    }
}
