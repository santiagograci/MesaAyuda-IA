using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Interfaces;
using MesaAyuda.Infrastructure.ExternalServices;
using MesaAyuda.Infrastructure.Persistence;
using MesaAyuda.Infrastructure.Persistence.Repositories;
using MesaAyuda.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace MesaAyuda.Infrastructure;

/// <summary>
/// Registro de servicios de persistencia, seguridad JWT y repositorios de MesaAyuda.Infrastructure.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Resuelve la cadena de conexión PostgreSQL desde <c>ConnectionStrings</c> o variables de entorno.
    /// </summary>
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var host = configuration["POSTGRES_HOST"] ?? "localhost";
        var port = configuration["POSTGRES_PORT"] ?? "5432";
        var db = configuration["POSTGRES_DB"] ?? "mesaayuda";
        var user = configuration["POSTGRES_USER"] ?? "mesaayuda_user";
        var pass = configuration["POSTGRES_PASSWORD"] ?? "mesaayuda_pass_2026";

        return $"Host={host};Port={port};Database={db};Username={user};Password={pass}";
    }

    /// <summary>
    /// Registra DbContext PostgreSQL, repositorios, UnitOfWork, servicios criptográficos JWT
    /// y el cliente HTTP resiliente del servicio RAG con extensibilidad opcional de handlers.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IHttpClientBuilder>? configureRagHttpClient = null)
    {
        var connectionString = ResolveConnectionString(configuration);
        var jwtSettings = JwtSettings.FromConfiguration(configuration);

        services.AddSingleton(jwtSettings);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICasoRepository, CasoRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var ragBaseUrl = (configuration["RAG_SERVICE_URL"] ?? "http://rag-service:8000").TrimEnd('/');
        var ragClientBuilder = services.AddHttpClient<IRagServiceClient, RagServiceClient>(client =>
        {
            client.BaseAddress = new Uri(ragBaseUrl);
        });

        configureRagHttpClient?.Invoke(ragClientBuilder);

        ragClientBuilder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(35);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
        });

        return services;
    }
}
