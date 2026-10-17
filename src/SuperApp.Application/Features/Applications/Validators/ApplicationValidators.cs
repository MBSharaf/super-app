using FluentValidation;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Applications.Dtos;

namespace SuperApp.Application.Features.Applications.Validators;

/// <summary>Rules shared by the three applications.</summary>
internal static class ApplicationRules
{
    public static readonly string[] PayoutMethods = ["Bank", "InstaPay"];

    public static IRuleBuilderOptions<T, string> NationalId<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Please enter your national ID number.")
        .Matches("^[23][0-9]{13}$").WithMessage("The national ID number has 14 digits and starts with 2 or 3.");

    public static IRuleBuilderOptions<T, string> PayoutMethod<T>(this IRuleBuilder<T, string> rule) => rule
        .Must(m => PayoutMethods.Contains(m)).WithMessage("Choose how we pay you: Bank or InstaPay.");

    public static IRuleBuilderOptions<T, string> PayoutDetail<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Where should we send your money?").Length(4, 200);

    public static IRuleBuilderOptions<T, bool> Accepted<T>(this IRuleBuilder<T, bool> rule) => rule
        .Equal(true).WithMessage("Please accept the agreement to continue.");
}

public sealed class PitchOwnerAnswersValidator : AbstractValidator<PitchOwnerAnswers>
{
    public PitchOwnerAnswersValidator()
    {
        RuleFor(x => x.OwnerName).PersonName().Must(n => n.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .WithMessage("Your full name, as on your ID.");
        RuleFor(x => x.Phone).EgyptianMobile();
        RuleFor(x => x.NationalId).NationalId();
        RuleFor(x => x.PitchName).NotEmpty().WithMessage("The name customers will see.").Length(3, 120);
        RuleFor(x => x.AreaId).GreaterThan(0).WithMessage("Choose the area.");
        RuleFor(x => x.Address).NotEmpty().WithMessage("Street and landmark.").Length(6, 300);
        RuleFor(x => x.Lat).InclusiveBetween(22, 32).WithMessage("Pin the pitch on the map (inside Egypt).");
        RuleFor(x => x.Lng).InclusiveBetween(24, 37).WithMessage("Pin the pitch on the map (inside Egypt).");
        RuleFor(x => x.Count).InclusiveBetween(1, 20).WithMessage("From 1 to 20 pitches.");
        RuleFor(x => x.Formats).NotEmpty().WithMessage("Choose at least one format.")
            .Must(f => f.All(v => v is 5 or 7 or 11)).WithMessage("Formats are 5, 7 or 11-a-side.");
        RuleFor(x => x.Price).InclusiveBetween(100, 5000).WithMessage("Between 100 and 5,000 EGP an hour.");
        RuleFor(x => x.PayoutMethod).PayoutMethod();
        RuleFor(x => x.PayoutDetail).PayoutDetail();
        RuleFor(x => x.AcceptTerms).Accepted();
        RuleFor(x => x.Note).MaximumLength(400);
    }
}

public sealed class SellerAnswersValidator : AbstractValidator<SellerAnswers>
{
    public SellerAnswersValidator()
    {
        RuleFor(x => x.FullName).PersonName();
        RuleFor(x => x.Phone).EgyptianMobile();
        RuleFor(x => x.NationalId).NationalId();
        RuleFor(x => x.StoreName).NotEmpty().WithMessage("Please name your store.").Length(2, 40);
        RuleFor(x => x.Handle!).Matches("^[a-z0-9](?:[a-z0-9-]{1,28})[a-z0-9]$")
            .WithMessage("Link name: 3–30 small letters, numbers or dashes.").When(x => !string.IsNullOrEmpty(x.Handle));
        RuleFor(x => x.Mode).Must(m => m is "pod" or "store" or "both").WithMessage("Choose how you want to sell.");
        RuleFor(x => x.Bio).MaximumLength(400);
        RuleFor(x => x.PayoutMethod).PayoutMethod();
        RuleFor(x => x.PayoutDetail).PayoutDetail();
        RuleFor(x => x.AcceptTerms).Accepted();
    }
}

public sealed class FactoryAnswersValidator : AbstractValidator<FactoryAnswers>
{
    private static readonly string[] Allowed = ["DTF", "DTG", "Screen", "Embroidery", "Sublimation"];

    public FactoryAnswersValidator()
    {
        RuleFor(x => x.OwnerName).PersonName();
        RuleFor(x => x.Phone).EgyptianMobile();
        RuleFor(x => x.NationalId).NationalId();
        RuleFor(x => x.FactoryName).NotEmpty().WithMessage("The factory name.").Length(2, 120);
        RuleFor(x => x.AreaId).GreaterThan(0).WithMessage("Choose the area.");
        RuleFor(x => x.Address).NotEmpty().Length(6, 300);
        RuleFor(x => x.CapacityPerDay).InclusiveBetween(1, 5000).WithMessage("Pieces a day: 1 to 5,000.");
        RuleFor(x => x.Techniques).NotEmpty().WithMessage("Choose at least one printing technique.")
            .Must(t => t.All(Allowed.Contains)).WithMessage("Techniques: DTF, DTG, Screen, Embroidery or Sublimation.");
        RuleFor(x => x.PayoutDetail).PayoutDetail();
        RuleFor(x => x.AcceptTerms).Accepted();
    }
}

public sealed class RejectRequestValidator : AbstractValidator<RejectRequest>
{
    public RejectRequestValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Please write why — the applicant will see it.").Length(5, 400);
}
