using Logicc.Api.Middleware;
using Logicc.AuditLogLib.Actors;

namespace Logicc.Api.Auth;

/// <summary>
/// <see cref="IActorContextProvider"/> implementation backed by the current HTTP request.
/// Reads the <see cref="ActorContext"/> that <see cref="RoleSimulationMiddleware"/> stashed on
/// <see cref="HttpContext.Items"/> for this request.
/// </summary>
public class HttpContextActorContextProvider : IActorContextProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextActorContextProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ActorContext? Context =>
        _httpContextAccessor.HttpContext?.Items.TryGetValue(RoleSimulationMiddleware.ActorContextItemKey, out var value) == true
            ? value as ActorContext
            : null;

    public Guid TenantId => Context is ITenantActor tenantActor ? tenantActor.TenantId : Guid.Empty;

    public Guid UserId => Context is IUserActor userActor ? userActor.UserId : Guid.Empty;

    public T Get<T>() where T : ActorContext =>
        Context as T ?? throw new InvalidOperationException(
            $"The current actor context is not of type {typeof(T).Name}.");

    public bool TryGet<T>(out T? context) where T : ActorContext
    {
        context = Context as T;
        return context is not null;
    }
}
