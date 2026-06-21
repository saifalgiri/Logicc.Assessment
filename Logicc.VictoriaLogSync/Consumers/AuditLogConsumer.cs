using Logicc.AuditLogLib.Contracts;
using Logicc.VictoriaLogSync.Clients;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Logicc.VictoriaLogSync.Consumers;


/// <summary>
/// MassTransit consumer that forwards <see cref="AuditLogMessage"/> events from RabbitMQ to VictoriaLogs.
///
/// Retry behaviour:
/// - MassTransit retries the entire <see cref="Consume"/> operation up to 3 times with a
///   5-second interval if it throws (configured via UseMessageRetry in Program.cs).
/// - The HTTP call to VictoriaLogs inside <see cref="IVictoriaLogsClient.SendAsync"/> is a
///   single attempt with no retry of its own. If it fails, the exception propagates back to
///   MassTransit, which decides whether to retry the consume operation.
/// </summary>
public class AuditLogConsumer : IConsumer<AuditLogMessage>
{
    private readonly IVictoriaLogsClient _victoriaLogsClient;
    private readonly ILogger<AuditLogConsumer> _logger;

    public AuditLogConsumer(IVictoriaLogsClient victoriaLogsClient, ILogger<AuditLogConsumer> logger)
    {
        _victoriaLogsClient = victoriaLogsClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuditLogMessage> context)
    {
        var message = context.Message;

        _logger.LogInformation(
            "Received audit log {AuditLogId}: {Action} {EntityName}/{EntityId} by {ActorType}:{ActorIdentifier}.",
            message.Id, message.Action, message.EntityName, message.EntityId, message.ActorType, message.ActorIdentifier);

        // Single HTTP attempt — no retry inside the client itself.
        // If this throws, the exception propagates to MassTransit, which retries
        // the entire Consume() operation (up to 3 times, 5-second backoff).
        await _victoriaLogsClient.SendAsync(message, context.CancellationToken);
    }
}
