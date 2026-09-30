using FluentValidation;

namespace DepotFlow.Application.ShippingLines;

public sealed class CreateShippingLineRequestValidator : AbstractValidator<CreateShippingLineRequest>
{
    public CreateShippingLineRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(2, 10)
            .Matches("^[A-Za-z0-9]*$").WithMessage("Code may contain letters and digits only.");

        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
    }
}

public sealed class UpdateShippingLineRequestValidator : AbstractValidator<UpdateShippingLineRequest>
{
    public UpdateShippingLineRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
    }
}
