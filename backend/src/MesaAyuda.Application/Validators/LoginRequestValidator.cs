using FluentValidation;
using MesaAyuda.Application.DTOs;

namespace MesaAyuda.Application.Validators;

/// <summary>
/// Reglas de validación para las solicitudes de inicio de sesión.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
