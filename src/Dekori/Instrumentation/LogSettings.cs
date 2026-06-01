using Microsoft.Extensions.Logging;

namespace Dekori.Instrumentation;

/// <summary>Log behavior settings resolved for a method.</summary>
internal sealed record LogSettings(LogLevel Level, bool LogArguments, bool LogResult);
