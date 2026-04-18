using FluentValidation;
using OpsDash.Application.DTOs.Users;

namespace OpsDash.Application.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        When(x => x.FirstName is not null, () => RuleFor(x => x.FirstName!).MaximumLength(100));
        When(x => x.LastName is not null, () => RuleFor(x => x.LastName!).MaximumLength(100));
        When(x => x.RoleId.HasValue, () => RuleFor(x => x.RoleId!.Value).GreaterThan(0));
    }
}
