using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shelter.BuildingBlocks.Tenancy;

namespace Shelter.BuildingBlocks.Persistence;

/// <summary>
/// Wraps every module endpoint of a tenant-scoped request in a <see cref="UnitOfWork"/>: commits on a success
/// result, rolls back on an error status or exception. Requests without a tenant run without a transaction and
/// see no tenant rows (RLS fails closed).
/// </summary>
public sealed class UnitOfWorkEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var services = context.HttpContext.RequestServices;
        if (services.GetRequiredService<ITenantContext>().TenantId is null)
        {
            return await next(context);
        }

        return await services.GetRequiredService<UnitOfWork>().ExecuteAsync(
            async _ => await next(context),
            result => IsSuccess(result, context.HttpContext),
            context.HttpContext.RequestAborted);
    }

    private static bool IsSuccess(object? result, HttpContext httpContext) =>
        result is IStatusCodeHttpResult { StatusCode: { } status }
            ? status < StatusCodes.Status400BadRequest
            : httpContext.Response.StatusCode < StatusCodes.Status400BadRequest;
}
