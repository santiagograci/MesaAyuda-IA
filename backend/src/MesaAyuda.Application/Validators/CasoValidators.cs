using FluentValidation;
using MesaAyuda.Application.DTOs;
using MesaAyuda.Domain.Enums;

namespace MesaAyuda.Application.Validators;

/// <summary>
/// Validador FluentValidation para la creación de casos de soporte.
/// </summary>
public class CrearCasoRequestValidator : AbstractValidator<CrearCasoRequest>
{
    public CrearCasoRequestValidator()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().WithMessage("El título del caso es obligatorio.")
            .Length(5, 150).WithMessage("El título debe tener entre 5 y 150 caracteres.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción del caso es obligatoria.")
            .Length(15, 2000).WithMessage("La descripción debe tener entre 15 y 2000 caracteres.");

        RuleFor(x => x.Prioridad)
            .NotEmpty().WithMessage("La prioridad es obligatoria.")
            .Must(BeValidPriority)
            .WithMessage("La prioridad debe ser Baja, Media, Alta o Critica.");
    }

    private static bool BeValidPriority(string prioridad) =>
        Enum.TryParse<PrioridadCaso>(prioridad, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);
}

/// <summary>
/// Validador FluentValidation para la actualización de datos de un caso.
/// </summary>
public class ActualizarCasoRequestValidator : AbstractValidator<ActualizarCasoRequest>
{
    public ActualizarCasoRequestValidator()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty().WithMessage("El título del caso es obligatorio.")
            .Length(5, 150).WithMessage("El título debe tener entre 5 y 150 caracteres.");

        RuleFor(x => x.Descripcion)
            .NotEmpty().WithMessage("La descripción del caso es obligatoria.")
            .Length(15, 2000).WithMessage("La descripción debe tener entre 15 y 2000 caracteres.");

        RuleFor(x => x.Prioridad)
            .NotEmpty().WithMessage("La prioridad es obligatoria.")
            .Must(BeValidPriority)
            .WithMessage("La prioridad debe ser Baja, Media, Alta o Critica.");
    }

    private static bool BeValidPriority(string prioridad) =>
        Enum.TryParse<PrioridadCaso>(prioridad, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);
}

/// <summary>
/// Validador FluentValidation para respuestas humanas registradas en un caso.
/// </summary>
public class CrearRespuestaRequestValidator : AbstractValidator<CrearRespuestaRequest>
{
    public CrearRespuestaRequestValidator()
    {
        RuleFor(x => x.Texto)
            .NotEmpty().WithMessage("El texto de la respuesta es obligatorio.")
            .Length(5, 4000).WithMessage("La respuesta debe tener entre 5 y 4000 caracteres.");
    }
}

/// <summary>
/// Validador FluentValidation para la aprobación y edición de sugerencias generadas por IA.
/// </summary>
public class AprobarSugerenciaRequestValidator : AbstractValidator<AprobarSugerenciaRequest>
{
    public AprobarSugerenciaRequestValidator()
    {
        RuleFor(x => x.TextoFinal)
            .NotEmpty().WithMessage("El texto final de la respuesta es obligatorio.")
            .Length(5, 4000).WithMessage("El texto final debe tener entre 5 y 4000 caracteres.");
    }
}
