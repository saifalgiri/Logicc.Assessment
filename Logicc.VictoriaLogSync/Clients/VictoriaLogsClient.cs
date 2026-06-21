using System.Net.Http.Json;
using System.Text.Json;
using Logicc.AuditLogLib.Contracts;
using Logicc.VictoriaLogSync.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Logicc.VictoriaLogSync.Clients;

/// <summary>
/// Posts each audit event as a single JSON line to VictoriaLogs' ingestion endpoint.
///
/// A single HTTP request is made per call — no retry at the HTTP level.
/// If VictoriaLogs returns an unsuccessful status code or the request fails, an exception
/// is thrown so the caller (<see cref="Consumers.AuditLogConsumer"/>) propagates it to
/// MassTransit, which retries the entire consume operation (3 attempts, 5-second backoff).
/// </summary>
public class VictoriaLogsClient : IVictoriaLogsClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly VictoriaLogsOptions _options;
    private readonly ILogger<VictoriaLogsClient> _logger;

    public VictoriaLogsClient(HttpClient httpClient, IOptions<VictoriaLogsOptions> options, ILogger<VictoriaLogsClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(AuditLogMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = new
        {
            id = message.Id,
            timestamp = message.Timestamp,
            message = message.Description,
            action = message.Action,
            entityName = message.EntityName,
            entityId = message.EntityId,
            description = message.Description,
            actorType = message.ActorType.ToString(),
            actorIdentifier = message.ActorIdentifier,
        };

        using var response = await _httpClient.PostAsJsonAsync(_options.IngestPath, payload, SerializerOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "VictoriaLogs returned {StatusCode} for audit log {AuditLogId}: {Body}",
                response.StatusCode, message.Id, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Forwarded audit log {AuditLogId} to VictoriaLogs.", message.Id);
    }
}
