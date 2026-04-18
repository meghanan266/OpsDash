using FluentValidation;
using OpsDash.Application.DTOs.Auth;

namespace OpsDash.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.Subdomain).NotEmpty();
    }
}
