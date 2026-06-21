using Logicc.AuditLogLib.Contracts;

namespace Logicc.AuditLogLib.Services;

/// <summary>
/// Generates and publishes audit log events for admin write operations.
///
/// Implementations must guarantee that non-admin actors never produce an audit event.
/// When a publish to RabbitMQ fails, the message must be written to the filesystem
/// (e.g. audit-failures/2026-06-21-14/) so the scheduler/worker can recover it.
/// No retries are performed by this library — a single publish attempt is made.
/// </summary>
public interface IAdminLogService
{
    /// <summary>
    /// Records a CREATE operation. No-op (and no publish) if the current actor is not an admin.
    /// </summary>
    Task LogCreateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an UPDATE operation. No-op (and no publish) if the current actor is not an admin.
    /// </summary>
    Task LogUpdateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a DELETE operation. No-op (and no publish) if the current actor is not an admin.
    /// </summary>
    Task LogDeleteAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an already-constructed <see cref="AuditLogMessage"/> to RabbitMQ.
    /// A single attempt is made — no retries. If publishing fails, the caller is responsible
    /// for persisting the message to the filesystem for later recovery by the scheduler/worker.
    /// </summary>
    Task PublishAsync(AuditLogMessage message, CancellationToken cancellationToken = default);
}
