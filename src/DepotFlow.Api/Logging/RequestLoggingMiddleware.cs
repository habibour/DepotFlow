using System.Diagnostics;

namespace DepotFlow.Api.Logging;

/// <summary>
/// Logs one line per request: method, path, status and duration. Only the path is logged, never the query string,
/// headers or body, so tokens and passwords cannot end up in the logs. Health checks log at Debug so probes do not
/// flood the output. The ASP.NET Core logging scope adds the trace id, which is the same id returned as "traceId"
/// in problem responses, so a reported error can be found in the logs.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var level = context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug : LogLevel.Information;

            logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode, elapsedMs);
        }
    }
}
