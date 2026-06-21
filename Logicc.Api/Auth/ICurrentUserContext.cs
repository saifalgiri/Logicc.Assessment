namespace Logicc.Api.Auth;

/// <summary>
/// Lightweight, request-scoped view of "who is calling" for authorization checks
/// (as opposed to <c>IActorContextProvider</c>, which carries the richer actor model
/// used for audit logging).
/// </summary>
public interface ICurrentUserContext
{
    bool IsAdmin { get; }

    string Role { get; }
}
