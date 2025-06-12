using System.Security.Claims;

namespace CurrencyConverter.API.Middleware;

/// <summary>
/// Request logging middleware that logs details of incoming HTTP requests.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Log the details of the incoming HTTP request, including method, endpoint, client IP, and response time.
    /// </summary>
    /// <param name="context"></param>
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        var clientIp = context.Connection.RemoteIpAddress?.ToString();
        var clientId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var method = context.Request.Method;
        var endpoint = context.Request.Path;

        try
        {
            await _next(context);
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("Request: {Method} {Endpoint} by ClientId {ClientId} from {ClientIp} returned {StatusCode} in {ResponseTime}ms",
                method, endpoint, clientId, clientIp, context.Response.StatusCode, responseTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request: {Method} {Endpoint} by ClientId {ClientId} from {ClientIp} failed", method, endpoint, clientId, clientIp);
        }
    }
}