using Logicc.AuditLogLib.Actors;
using Logicc.AuditLogLib.Contracts;
using Logicc.AuditLogLib.IServices;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Logicc.AuditLogLib.Services;

/// <summary>
/// Default <see cref="IAdminLogService"/> implementation.
///
/// Responsibilities:
///   1. Determine the current actor via <see cref="IActorContextProvider"/>.
///   2. Build an <see cref="AuditLogMessage"/>.
///   3. Fire-and-forget: publish to RabbitMQ without blocking the caller.
///   4. If publishing fails, persist the message via <see cref="IFileBufferLogger"/>
///      so <see cref="Workers.FileBufferWorker"/> can re-publish it later.
///
/// Audit events are only ever produced when the current actor is an <see cref="AdminActorContext"/>.
/// Every other actor type (including unauthenticated requests) is silently skipped, by design —
/// non-admin actors must never generate an audit log.
/// </summary>
public class AdminLogService : IAdminLogService
{
    private readonly IActorContextProvider _actorContextProvider;
    private readonly IBus _bus;
    private readonly IFileBufferLogger _fileBufferLogger;
    private readonly ILogger<AdminLogService> _logger;

    public AdminLogService(
        IActorContextProvider actorContextProvider,
        IBus bus,
        IFileBufferLogger fileBufferLogger,
        ILogger<AdminLogService> logger)
    {
        _actorContextProvider = actorContextProvider;
        _bus = bus;
        _fileBufferLogger = fileBufferLogger;
        _logger = logger;
    }

    public Task LogCreateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
        => LogIfAdminAsync("Create", entityName, entityId, description);

    public Task LogUpdateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
        => LogIfAdminAsync("Update", entityName, entityId, description);

    public Task LogDeleteAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
        => LogIfAdminAsync("Delete", entityName, entityId, description);

    private Task LogIfAdminAsync(string action, string entityName, string entityId, string description)
    {
        if (_actorContextProvider.Context is not AdminActorContext adminActor)
        {
            _logger.LogDebug(
                "Skipping audit log for {Action} {EntityName}/{EntityId}: current actor is not an admin.",
                action, entityName, entityId);
            return Task.CompletedTask;
        }

        var (actorType, actorIdentifier) = MapActor(adminActor);

        var message = new AuditLogMessage
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            ActorType = actorType,
            ActorIdentifier = actorIdentifier,
        };

        // Fire-and-forget: publish without blocking the HTTP request.
        // CancellationToken.None is intentional — this work outlives the request.
        _ = PublishWithFallbackAsync(message);

        return Task.CompletedTask;
    }

    private async Task PublishWithFallbackAsync(AuditLogMessage message)
    {
        try
        {
            await _bus.Publish(message, CancellationToken.None);

            _logger.LogInformation(
                "Published audit log {AuditLogId}: {Action} {EntityName}/{EntityId} by {ActorType}:{ActorIdentifier}.",
                message.Id, message.Action, message.EntityName, message.EntityId, message.ActorType, message.ActorIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish audit log {AuditLogId}: {Action} {EntityName}/{EntityId}. Writing to file buffer.",
                message.Id, message.Action, message.EntityName, message.EntityId);

            try
            {
                await _fileBufferLogger.WriteAsync(message, CancellationToken.None);
            }
            catch (Exception fileEx)
            {
                _logger.LogError(
                    fileEx,
                    "Failed to write audit log {AuditLogId} to file buffer. Event may be lost.",
                    message.Id);
            }
        }
    }

    public async Task PublishAsync(AuditLogMessage message, CancellationToken cancellationToken = default)
    {
        if (message is null) return;

        try
        {
            // Single publish attempt — no retries are performed by this library.
            await _bus.Publish(message, cancellationToken);

            _logger.LogInformation(
                "Published audit log {AuditLogId}: {Action} {EntityName}/{EntityId} by {ActorType}:{ActorIdentifier}.",
                message.Id, message.Action, message.EntityName, message.EntityId, message.ActorType, message.ActorIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish audit log {AuditLogId}: {Action} {EntityName}/{EntityId} by {ActorType}:{ActorIdentifier}. Message must be persisted to disk for later recovery.",
                message.Id, message.Action, message.EntityName, message.EntityId, message.ActorType, message.ActorIdentifier);
            throw;
        }
    }

    /// <summary>
    /// Maps a concrete <see cref="ActorContext"/> to the (ActorType, ActorIdentifier) pair stored
    /// on the audit message. Order matters: more specific types (e.g. TenantMemberActorContext,
    /// which derives from UserActorContext) must be matched before their base types.
    /// </summary>
    internal static (ActorType ActorType, string ActorIdentifier) MapActor(ActorContext context) => context switch
    {
        AdminActorContext admin => (ActorType.Admin, admin.Id),
        TenantMemberActorContext tenantMember => (ActorType.TenantMember, tenantMember.UserId.ToString()),
        ApiKeyActorContext apiKey => (ActorType.ApiKey, apiKey.TenantId.ToString()),
        ServiceActorContext service => (ActorType.Service, service.ServiceName),
        UserActorContext user => (ActorType.User, user.UserId.ToString()),
        _ => throw new InvalidOperationException($"Unsupported actor context type: {context.GetType().Name}"),
    };
}
