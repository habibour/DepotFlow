using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DepotFlow.Api.Tests.Support;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, Exception? Exception, IReadOnlyList<string> Scopes);

/// <summary>Collects every log entry together with its scopes (ASP.NET Core puts the trace id in a scope).</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<string>();
            owner._scopes.ForEachScope((scope, list) => list.Add(scope?.ToString() ?? ""), scopes);
            // Structured scopes (like the trace id) print their key/value pairs through ToString for the built-in types.
            owner._scopes.ForEachScope((scope, list) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
                {
                    list.AddRange(pairs.Select(p => $"{p.Key}={p.Value}"));
                }
            }, scopes);

            owner.Entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), exception, scopes));
        }
    }
}
