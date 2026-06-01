using System.Reflection;
using Castle.DynamicProxy;
using Dekori.Instrumentation;
using Dekori.Tests.Support;
using NSubstitute;
using Shouldly;

namespace Dekori.Tests;

/// <summary>
/// Unit tests for plan resolution and caching, driving the cache directly with a substituted
/// <see cref="IInvocation"/> — no proxy or telemetry pipeline involved.
/// </summary>
public sealed class InstrumentationPlanCacheTests
{
    private readonly InstrumentationPlanCache _cache = new(new DekoriOptions());

    private static IInvocation InvocationFor(Type targetType, string methodName)
    {
        MethodInfo method = targetType.GetMethod(methodName)!;
        var invocation = Substitute.For<IInvocation>();
        invocation.MethodInvocationTarget.Returns(method);
        invocation.TargetType.Returns(targetType);
        return invocation;
    }

    [Fact]
    public void Method_level_trace_attribute_yields_a_trace_plan()
    {
        var plan = _cache.GetPlan(InvocationFor(typeof(Greeter), nameof(Greeter.Greet)));

        plan.IsInstrumented.ShouldBeTrue();
        plan.Trace.ShouldNotBeNull();
        plan.OperationName.ShouldBe("Greeter.Greet");
    }

    [Fact]
    public void Class_level_instrument_attribute_enables_the_default_plan()
    {
        var plan = _cache.GetPlan(InvocationFor(typeof(Mixed), nameof(Mixed.Tracked)));

        plan.Trace.ShouldNotBeNull();
        plan.Metric.ShouldNotBeNull();
        plan.CaptureException.ShouldBeTrue();
    }

    [Fact]
    public void No_instrument_attribute_disables_instrumentation()
    {
        var plan = _cache.GetPlan(InvocationFor(typeof(Mixed), nameof(Mixed.Untracked)));

        plan.IsInstrumented.ShouldBeFalse();
    }

    [Fact]
    public void Plan_is_resolved_once_and_cached_per_method()
    {
        var invocation = InvocationFor(typeof(Greeter), nameof(Greeter.Greet));

        var first = _cache.GetPlan(invocation);
        var second = _cache.GetPlan(invocation);

        second.ShouldBeSameAs(first);
    }
}
