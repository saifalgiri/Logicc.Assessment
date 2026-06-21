using System.Collections.Concurrent;
using Logicc.AuditLogLib.Contracts;
using Logicc.AuditLogLib.IServices;

namespace Logicc.Test.Api.TestHelpers;

/// <summary>
/// Test double for <see cref="IAdminLogService"/> that records every call instead of publishing
/// to a real message bus. Lets integration tests assert "an audit log was (or wasn't) produced"
/// without requiring a running RabbitMQ broker.
/// </summary>
public class RecordingAdminLogService : IAdminLogService
{
    public record Entry(string Operation, string EntityName, string EntityId, string Description);

    private readonly ConcurrentQueue<Entry> _entries = new();
    private readonly ConcurrentQueue<AuditLogMessage> _publishedMessages = new();

    public IReadOnlyCollection<Entry> Entries => _entries.ToArray();

    public IReadOnlyCollection<AuditLogMessage> PublishedMessages => _publishedMessages.ToArray();

    public Task LogCreateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(new Entry("Create", entityName, entityId, description));
        return Task.CompletedTask;
    }

    public Task LogUpdateAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(new Entry("Update", entityName, entityId, description));
        return Task.CompletedTask;
    }

    public Task LogDeleteAsync(string entityName, string entityId, string description, CancellationToken cancellationToken = default)
    {
        _entries.Enqueue(new Entry("Delete", entityName, entityId, description));
        return Task.CompletedTask;
    }

    public Task PublishAsync(AuditLogMessage message, CancellationToken cancellationToken = default)
    {
        _publishedMessages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _entries.Clear();
        _publishedMessages.Clear();
    }
}
