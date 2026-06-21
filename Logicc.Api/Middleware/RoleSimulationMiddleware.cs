using Logicc.Api.Auth;
using Logicc.AuditLogLib.Actors;

namespace Logicc.Api.Middleware;

/// <summary>
/// Simulates authentication/authorization based on a request header, since this assessment
/// does not use ASP.NET Identity. Reads the "x-role" header ("admin" or "user", defaulting to
/// "user" when absent) and:
///
///   1. Populates the request-scoped <see cref="CurrentUserContext"/> (consumed by
///      <see cref="Logicc.Api.Filters.AdminOnlyAttribute"/> for endpoint authorization).
///   2. Builds the richer <see cref="ActorContext"/> used by the audit-logging pipeline
///      (an <see cref="AdminActorContext"/> for "admin", a <see cref="UserActorContext"/>
///      otherwise) and stores it on <see cref="HttpContext.Items"/>, where it is picked up by
///      <see cref="Logicc.Api.Auth.HttpContextActorContextProvider"/> via dependency injection.
/// </summary>
public class RoleSimulationMiddleware
{
    public const string RoleHeaderName = "x-role";
    public const string ActorIdHeaderName = "x-actor-id";
    public const string UserIdHeaderName = "x-user-id";

    /// <summary>
    /// Key used to stash the resolved <see cref="ActorContext"/> on <see cref="HttpContext.Items"/>.
    /// </summary>
    internal const string ActorContextItemKey = "Logicc.AuditLogLib.ActorContext";

    private static readonly Guid DefaultSimulatedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly RequestDelegate _next;
    private readonly ILogger<RoleSimulationMiddleware> _logger;

    public RoleSimulationMiddleware(RequestDelegate next, ILogger<RoleSimulationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, CurrentUserContext currentUserContext)
    {
        var rawRole = httpContext.Request.Headers[RoleHeaderName].ToString();
        var role = string.IsNullOrWhiteSpace(rawRole) ? "user" : rawRole.Trim().ToLowerInvariant();

        currentUserContext.SetRole(role);

        var actorContext = BuildActorContext(httpContext, role);
        httpContext.Items[ActorContextItemKey] = actorContext;

        _logger.LogDebug(
            "Resolved role '{Role}' ({ActorType}) for {Method} {Path}.",
            role, actorContext.Type, httpContext.Request.Method, httpContext.Request.Path);

        await _next(httpContext);
    }

    private static ActorContext BuildActorContext(HttpContext httpContext, string role)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();

        if (role == "admin")
        {
            var actorId = httpContext.Request.Headers[ActorIdHeaderName].FirstOrDefault();

            return new AdminActorContext
            {
                Id = string.IsNullOrWhiteSpace(actorId) ? "admin-1" : actorId,
                Provider = AuthenticationProvider.Logicc,
                Ip = ip,
                UserAgent = userAgent,
            };
        }

        var userIdHeader = httpContext.Request.Headers[UserIdHeaderName].FirstOrDefault();
        var userId = Guid.TryParse(userIdHeader, out var parsedUserId) ? parsedUserId : DefaultSimulatedUserId;

        return new UserActorContext
        {
            UserId = userId,
            Provider = AuthenticationProvider.Logicc,
            TimeZoneMetadata = null,
            Ip = ip,
            UserAgent = userAgent,
        };
    }
}
