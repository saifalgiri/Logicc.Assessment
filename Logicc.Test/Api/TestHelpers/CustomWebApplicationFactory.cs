using Logicc.Api.Data;
using Logicc.AuditLogLib.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Logicc.Test.Api.TestHelpers;

/// <summary>
/// Boots Logicc.Api in-memory for integration testing:
///   - Uses the in-memory <see cref="InMemoryProductRepository"/> for data storage,
///     so tests don't need a database.
///   - Swaps the real <see cref="IAdminLogService"/> for <see cref="RecordingAdminLogService"/>,
///     so tests can assert on audit-log behavior without a running RabbitMQ broker.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public RecordingAdminLogService RecordingAuditLogService { get; } = new();
    public InMemoryProductRepository? ProductRepository { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the real audit service with a recording one for testing
            services.RemoveAll<IAdminLogService>();
            services.AddSingleton<IAdminLogService>(RecordingAuditLogService);

            // Note: InMemoryProductRepository is registered as a singleton in Program.cs,
            // so it will maintain state across requests within a test. We capture the
            // reference so tests can reset it if needed.
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Get reference to the repository for test cleanup
        using var scope = host.Services.CreateScope();
        ProductRepository = scope.ServiceProvider.GetRequiredService<InMemoryProductRepository>();

        return host;
    }
}
