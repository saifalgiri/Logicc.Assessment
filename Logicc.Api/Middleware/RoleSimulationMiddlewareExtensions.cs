namespace Logicc.Api.Middleware;

public static class RoleSimulationMiddlewareExtensions
{
    /// <summary>
    /// Adds <see cref="RoleSimulationMiddleware"/> to the pipeline. Must run before any
    /// middleware/filter/endpoint that depends on <c>ICurrentUserContext</c> or
    /// <c>IActorContextProvider</c>.
    /// </summary>
    public static IApplicationBuilder UseRoleSimulation(this IApplicationBuilder app)
        => app.UseMiddleware<RoleSimulationMiddleware>();
}
