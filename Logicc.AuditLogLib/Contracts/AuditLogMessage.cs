using Logicc.AuditLogLib.Actors;

namespace Logicc.AuditLogLib.Contracts;

/// <summary>
/// Message contract published to RabbitMQ (via MassTransit) whenever an admin performs
/// a write operation. Consumed by Logicc.VictoriaLogSync and forwarded to VictoriaLogs.
/// </summary>
public class AuditLogMessage
{
    public Guid Id { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>
    /// "Create", "Update" or "Delete".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The name of the entity that was written to (e.g. "Product").
    /// </summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>
    /// The primary key of the affected entity, as a string.
    /// </summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what happened.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public ActorType ActorType { get; set; }

    /// <summary>
    /// An identifier for the actor that performed the action (admin id, user id, tenant id, service name, etc.)
    /// depending on <see cref="ActorType"/>.
    /// </summary>
    public string ActorIdentifier { get; set; } = string.Empty;
}
