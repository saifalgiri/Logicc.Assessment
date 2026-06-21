using Logicc.AuditLogLib.Configuration;
using Logicc.AuditLogLib.Services;
using Logicc.AuditLogLib.Workers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Logicc.AuditLogLib.DependencyInjection;


/// <summary>
/// Registers audit logging services and the MassTransit/RabbitMQ publisher.
///
/// Publishing strategy: fire-and-forget — Log*Async methods return immediately without blocking
/// the caller. A single publish attempt is made in the background; on failure the message is
/// persisted to disk by <see cref="FileBufferLogger"/> and re-published by <see cref="FileBufferWorker"/>.
/// </summary>
public static class AuditLogServiceCollectionExtensions
{

    public static IServiceCollection AddAuditLogging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFileBufferLogger, FileBufferLogger>();
        services.AddHostedService<FileBufferWorker>();
        services.AddScoped<IAdminLogService, AdminLogService>();

        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetKebabCaseEndpointNameFormatter();

            busConfigurator.UsingRabbitMq((context, rabbitCfg) =>
            {
                var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

                rabbitCfg.Host(options.Host, options.VirtualHost, hostCfg =>
                {
                    hostCfg.Username(options.Username);
                    hostCfg.Password(options.Password);
                });

                rabbitCfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
