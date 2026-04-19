using FluentValidation;
using OpsDash.Application.DTOs.Auth;

namespace OpsDash.Application.Validators;

public sealed class TokenRevokeRequestValidator : AbstractValidator<TokenRevokeRequest>
{
    public TokenRevokeRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required");
    }
}
