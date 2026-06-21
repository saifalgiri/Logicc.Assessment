using Logicc.Api.Auth;
using Logicc.Api.Data;
using Logicc.Api.IServices;
using Logicc.Api.Middleware;
using Logicc.Api.Services;
using Logicc.AuditLogLib.Actors;
using Logicc.AuditLogLib.DependencyInjection;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// MVC / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Logicc.Assessment API",
        Version = "v1",
        Description =
            "Demonstrates audit logging for admin-performed write operations. " +
            "Set the 'x-role' request header to 'admin' or 'user' to simulate a caller.",
    });

    options.AddSecurityDefinition("x-role", new OpenApiSecurityScheme
    {
        Name = RoleSimulationMiddleware.RoleHeaderName,
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Description = "Simulated role: 'admin' or 'user'.",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "x-role"
                }
            },
            new string[] { }
        }
    });
});

// In-memory data store and service registrations
builder.Services.AddSingleton<InMemoryProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

// Role simulation / actor resolution
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserContext>();
builder.Services.AddScoped<ICurrentUserContext>(sp => sp.GetRequiredService<CurrentUserContext>());
builder.Services.AddScoped<IActorContextProvider, HttpContextActorContextProvider>();

// Audit logging (MassTransit + RabbitMQ), configured from this app's appsettings.json.
builder.Services.AddAuditLogging(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Must run before anything that depends on ICurrentUserContext / IActorContextProvider.
app.UseRoleSimulation();

app.MapControllers();

app.Run();

/// <summary>
/// Exposed so Logicc.Test can host this application via WebApplicationFactory<Program>
/// for integration tests.
/// </summary>
public partial class Program;
