namespace Logicc.AuditLogLib.Actors;

/// <summary>
/// Subscription tier associated with a tenant. Used by <see cref="TenantMemberActorContext"/>.
/// </summary>
public enum ProductTier : byte
{
    Unspecified = 0,
    Trial = 1,
    Business = 2,
    SecurePlus = 3,
    Max = 4,
}

/// <summary>
/// The kind of actor that performed an action against the system.
/// </summary>
public enum ActorType
{
    User,
    TenantMember,
    ApiKey,
    Admin,
    Service,
}

/// <summary>
/// The identity provider that authenticated the current actor.
/// </summary>
public enum AuthenticationProvider
{
    Logicc,
    Clerk,
    Cloudflare,
    Slack,
    Stripe,
    BoldSign,
}

/// <summary>
/// Resolves the actor (caller) associated with the current logical operation (e.g. the current HTTP request).
/// </summary>
public interface IActorContextProvider
{
    bool IsAuthenticated => Context is not null;

    ActorContext? Context { get; }

    Guid TenantId { get; }

    Guid UserId { get; }

    T Get<T>() where T : ActorContext;

    bool TryGet<T>(out T? context) where T : ActorContext;
}

/// <summary>
/// Implemented by actor contexts that are scoped to a tenant.
/// </summary>
public interface ITenantActor
{
    Guid TenantId { get; }
}

/// <summary>
/// Implemented by actor contexts that represent a human user.
/// </summary>
public interface IUserActor
{
    Guid UserId { get; }
}

/// <summary>
/// Base type for every kind of actor that can perform an action in the system.
/// </summary>
public abstract record ActorContext(ActorType Type)
{
    public required AuthenticationProvider Provider { get; init; }
}

/// <summary>
/// Base type for actor contexts that originate from a web request.
/// </summary>
public abstract record WebActorContext(ActorType Type) : ActorContext(Type)
{
    public string? Ip { get; init; }

    public string? UserAgent { get; init; }
}

public record TimeZoneMetadata(TimeZoneInfo Info, string IanaName);

/// <summary>
/// A plain, non-tenant-scoped authenticated user (e.g. a "user" role in the role-simulation header).
/// </summary>
public record UserActorContext(ActorType Type = ActorType.User) : WebActorContext(Type), IUserActor
{
    public string? SessionId { get; init; }

    public required Guid UserId { get; init; }

    public required TimeZoneMetadata? TimeZoneMetadata { get; init; }
}

/// <summary>
/// A user acting within the context of a tenant/workspace.
/// </summary>
public record TenantMemberActorContext() : UserActorContext(ActorType.TenantMember), ITenantActor
{
    public required ProductTier ProductTier { get; init; }

    public required Guid TenantId { get; init; }
}

/// <summary>
/// A machine-to-machine caller authenticated via an API key, scoped to a tenant.
/// </summary>
public record ApiKeyActorContext() : WebActorContext(ActorType.ApiKey), ITenantActor
{
    public required Guid TenantId { get; init; }
}

/// <summary>
/// An administrator acting through the back-office/admin surface. The only actor type
/// permitted to trigger audit log generation.
/// </summary>
public record AdminActorContext() : WebActorContext(ActorType.Admin)
{
    public required string Id { get; init; }
}

/// <summary>
/// An internal, non-interactive caller (background job, scheduled task, etc.).
/// </summary>
public record ServiceActorContext() : ActorContext(ActorType.Service)
{
    public required string ServiceName { get; init; }
}
