using Logicc.AuditLogLib.Configuration;
using Logicc.VictoriaLogSync.Clients;
using Logicc.VictoriaLogSync.Configuration;
using Logicc.VictoriaLogSync.Consumers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<VictoriaLogsOptions>()
    .Bind(builder.Configuration.GetSection(VictoriaLogsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// VictoriaLogs HTTP client — single attempt per message, no retry policy.
// If the request fails, VictoriaLogsClient throws and AuditLogConsumer surfaces the error.
builder.Services.AddHttpClient<IVictoriaLogsClient, VictoriaLogsClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<VictoriaLogsOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    });

// MassTransit / RabbitMQ consumer
builder.Services.AddMassTransit(busConfigurator =>
{
    busConfigurator.SetKebabCaseEndpointNameFormatter();
    busConfigurator.AddConsumer<AuditLogConsumer>();

    busConfigurator.UsingRabbitMq((context, rabbitCfg) =>
    {
        var options = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;

        rabbitCfg.Host(options.Host, options.VirtualHost, hostCfg =>
        {
            hostCfg.Username(options.Username);
            hostCfg.Password(options.Password);
        });

        // Consumer retries the entire Consume() operation up to 3 times with a 5-second
        // interval between attempts. This is MassTransit-level retry — it re-invokes
        // AuditLogConsumer.Consume() on failure, not the HTTP call inside it.
        // The HTTP call to VictoriaLogs is a single attempt with no retry of its own.
        rabbitCfg.UseMessageRetry(retryCfg => retryCfg.Interval(3, TimeSpan.FromSeconds(5)));

        rabbitCfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
