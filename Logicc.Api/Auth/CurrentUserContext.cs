namespace Logicc.Api.Auth;

/// <summary>
/// Scoped (per-request) implementation of <see cref="ICurrentUserContext"/>.
/// Populated exclusively by <see cref="Logicc.Api.Middleware.RoleSimulationMiddleware"/> at the
/// start of the request pipeline, based on the "x-role" header.
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    public bool IsAdmin { get; private set; }

    public string Role { get; private set; } = "anonymous";

    /// <summary>
    /// Internal on purpose: only the role-simulation middleware (same assembly) should be able
    /// to set the current role for a request.
    /// </summary>
    internal void SetRole(string role)
    {
        Role = string.IsNullOrWhiteSpace(role) ? "anonymous" : role.Trim().ToLowerInvariant();
        IsAdmin = string.Equals(Role, "admin", StringComparison.OrdinalIgnoreCase);
    }
}
