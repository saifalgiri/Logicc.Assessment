using System.Text.Json;
using Logicc.AuditLogLib.Contracts;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logicc.AuditLogLib.Workers;

/// <summary>
/// Background worker that re-publishes audit log messages that previously failed to reach RabbitMQ.
/// Polls <c>audit-failures/</c> every 30 seconds. For each <c>.json</c> file found:
///   - deserialises the message
///   - publishes it to RabbitMQ (single attempt, no retry)
///   - deletes the file on success; leaves it on failure for the next cycle.
/// Uses <see cref="IBus"/> (singleton) rather than <see cref="IPublishEndpoint"/> (scoped)
/// so it can safely be injected into this singleton BackgroundService.
/// </summary>
public sealed class FileBufferWorker : BackgroundService
{
    private const string BaseDirectory = "audit-failures";
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);

    private readonly IBus _bus;
    private readonly ILogger<FileBufferWorker> _logger;

    public FileBufferWorker(IBus bus, ILogger<FileBufferWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBufferedFilesAsync(stoppingToken);

            try
            {
                await Task.Delay(PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBufferedFilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(BaseDirectory))
            return;

        var files = Directory.GetFiles(BaseDirectory, "*.json", SearchOption.AllDirectories);

        if (files.Length == 0)
            return;

        _logger.LogInformation("FileBufferWorker found {Count} buffered audit log(s) to re-publish.", files.Length);

        foreach (var filePath in files)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await TryRepublishAsync(filePath, cancellationToken);
        }
    }

    private async Task TryRepublishAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var message = JsonSerializer.Deserialize<AuditLogMessage>(json);

            if (message is null)
            {
                _logger.LogError(
                    "Skipping {FilePath}: file could not be deserialised into an AuditLogMessage.",
                    filePath);
                return;
            }

            await _bus.Publish(message, cancellationToken);

            File.Delete(filePath);

            _logger.LogInformation(
                "Re-published buffered audit log {AuditLogId} from {FilePath}.",
                message.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to re-publish buffered audit log from {FilePath}. Will retry on next cycle.",
                filePath);
        }
    }
}
