using Logicc.AuditLogLib.Contracts;

namespace Logicc.AuditLogLib.Services;

/// <summary>
/// Persists an <see cref="AuditLogMessage"/> to the local filesystem when RabbitMQ publishing fails.
/// The stored files are later picked up and re-published by <see cref="Workers.FileBufferWorker"/>.
/// </summary>
public interface IFileBufferLogger
{
    Task WriteAsync(AuditLogMessage message, CancellationToken cancellationToken = default);
}
