using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Shelter.BuildingBlocks.Auditing;

namespace Shelter.Host.Middleware;

/// <summary>
/// Assigns every request a correlation ID: a well-formed inbound <c>X-Correlation-Id</c>, otherwise the W3C trace ID.
/// The ID is echoed on the response, added to the logging scope, put on ProblemDetails, and stamped on the
/// request's audit events with source <c>api</c>.
/// </summary>
internal sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaxLength = 64;
    private static readonly object ItemKey = new();

    public async Task InvokeAsync(HttpContext context, AuditContext auditContext)
    {
        var correlationId = Resolve(context);
        context.Items[ItemKey] = correlationId;

        // TODO(M2): the authenticated staff user once authentication exists.
        auditContext.Set(AuditActorType.Anonymous, actorId: null, AuditSources.Api, correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }

    public static bool TryGet(HttpContext context, [NotNullWhen(true)] out string? correlationId)
    {
        correlationId = context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
        return correlationId is not null;
    }

    private static string Resolve(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();
        return IsWellFormed(inbound)
            ? inbound
            : Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
    }

    // ASCII letters, digits and - _ . : only, so arbitrary client input never reaches logs.
    private static bool IsWellFormed(string value) =>
        value.Length is > 0 and <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');
}
