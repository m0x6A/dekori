using Microsoft.Extensions.Logging;

namespace Dekori.Tests.Support;

/// <summary>A captured log entry.</summary>
public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);
