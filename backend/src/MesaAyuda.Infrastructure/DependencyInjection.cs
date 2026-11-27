using MesaAyuda.Application.Interfaces;
using MesaAyuda.Domain.Interfaces;
using MesaAyuda.Infrastructure.Persistence;
using MesaAyuda.Infrastructure.Persistence.Repositories;
using MesaAyuda.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// Registra DbContext PostgreSQL, repositorios, UnitOfWork y servicios criptográficos JWT.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
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

        return services;
    }
}
