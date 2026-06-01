using System.Diagnostics;

namespace Dekori.Tests.Support;

/// <summary>Helper to read a span tag value as a string.</summary>
public static class ActivityExtensions
{
    public static string? Tag(this Activity activity, string key) =>
        activity.GetTagItem(key) as string;
}
