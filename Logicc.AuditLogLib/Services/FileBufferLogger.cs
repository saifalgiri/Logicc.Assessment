using System.Text.Json;
using Logicc.AuditLogLib.Contracts;
using Microsoft.Extensions.Logging;

namespace Logicc.AuditLogLib.Services;

/// <summary>
/// Writes a failed <see cref="AuditLogMessage"/> to disk under
/// <c>audit-failures/yyyy-MM-dd-HH/{messageId}.json</c>.
/// Files are grouped by UTC hour so old directories are easy to prune.
/// </summary>
public sealed class FileBufferLogger : IFileBufferLogger
{
    private const string BaseDirectory = "audit-failures";
    private readonly ILogger<FileBufferLogger> _logger;

    public FileBufferLogger(ILogger<FileBufferLogger> logger)
    {
        _logger = logger;
    }

    public async Task WriteAsync(AuditLogMessage message, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(BaseDirectory, DateTime.UtcNow.ToString("yyyy-MM-dd-HH"));
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{message.Id}.json");
        var json = JsonSerializer.Serialize(message);

        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        _logger.LogWarning(
            "Audit log {AuditLogId} buffered to disk at {FilePath} for later re-publishing.",
            message.Id, filePath);
    }
}
