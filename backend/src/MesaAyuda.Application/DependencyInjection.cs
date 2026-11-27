using FluentValidation;
using MesaAyuda.Application.Interfaces;
using MesaAyuda.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MesaAyuda.Application;

/// <summary>
/// Registro de servicios y validadores de la capa de Aplicación en el contenedor IoC.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra los casos de uso y validadores FluentValidation de MesaAyuda.Application.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
