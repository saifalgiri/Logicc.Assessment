using Logicc.AuditLogLib.Contracts;

namespace Logicc.VictoriaLogSync.Clients;

/// <summary>
/// Sends audit log events to VictoriaLogs via HTTP.
///
/// A single HTTP request is made per call — no retry at this level. Throws on failure
/// so MassTransit can retry the entire consume operation (3 attempts, 5-second backoff).
/// </summary>
public interface IVictoriaLogsClient
{
    Task SendAsync(AuditLogMessage message, CancellationToken cancellationToken = default);
}
