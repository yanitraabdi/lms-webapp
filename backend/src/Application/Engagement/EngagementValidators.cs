using FluentValidation;

namespace Academy.Application.Engagement;

public class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public class FeedbackRequestValidator : AbstractValidator<FeedbackRequest>
{
    public FeedbackRequestValidator()
    {
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Context).MaximumLength(200);
    }
}
