using BodegaAbierta.Application.Abstractions;
using BodegaAbierta.Infrastructure.Auditing;
using BodegaAbierta.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BodegaAbierta.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "BodegaAbierta";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read lazily (when the first DbContext is built), so tests and
        // tooling can override configuration after Program.cs has run.
        services.AddDbContext<BodegaAbiertaDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is missing. Set it with dotnet user-secrets or the " +
                    $"ConnectionStrings__{ConnectionStringName} environment variable (see README / .env.example).");
            }

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());

            // Resolved from the same request scope, so it sees this request's user.
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<AuditSaveChangesInterceptor>();

        // Same scoped instance whether a class asks for the concrete context or the Application port.
        services.AddScoped<IBodegaDbContext>(sp => sp.GetRequiredService<BodegaAbiertaDbContext>());

        return services;
    }
}
