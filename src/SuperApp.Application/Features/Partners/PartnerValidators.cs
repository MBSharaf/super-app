using FluentValidation;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

public sealed class PitchRequestValidator : AbstractValidator<PitchRequest>
{
    public PitchRequestValidator()
    {
        RuleFor(x => x.VenueId).GreaterThan(0).WithMessage("Choose a venue.");
        RuleFor(x => x.Name).NotEmpty().Length(3, 120);
        RuleFor(x => x.Format).Must(f => f is 5 or 7 or 11).WithMessage("Formats are 5, 7 or 11-a-side.");
        RuleFor(x => x.Surface).NotEmpty().IsEnumName(typeof(PitchSurface), caseSensitive: true).WithMessage("Choose ArtificialTurf, NaturalGrass or IndoorCourt.");
        RuleFor(x => x.PricePerHour).InclusiveBetween(100, 5000).WithMessage("Between 100 and 5,000 EGP an hour.");
        RuleFor(x => x.PeakPricePerHour).InclusiveBetween(100, 7500).When(x => x.PeakPricePerHour is not null)
            .GreaterThanOrEqualTo(x => x.PricePerHour).When(x => x.PeakPricePerHour is not null).WithMessage("The peak price can’t be lower than the normal price.");
        RuleFor(x => x.PeakFromHour).NotNull().When(x => x.PeakPricePerHour is not null).WithMessage("Choose when the peak price starts.")
            .InclusiveBetween(12, 23).When(x => x.PeakFromHour is not null);
        RuleFor(x => x.DescriptionEn).MaximumLength(2000);
        RuleFor(x => x.DescriptionAr).MaximumLength(2000);
        RuleFor(x => x.PolicyKey).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Facilities).NotNull().Must(f => f.Count <= 20).WithMessage("Up to 20 facilities.");
        RuleFor(x => x.OpeningHours).NotNull()
            .Must(h => h.Count == 7 && h.Select(d => d.DayOfWeek).Distinct().Count() == 7 && h.All(d => d.DayOfWeek is >= 0 and <= 6))
            .WithMessage("Send the opening hours of all 7 days (0 = Sunday … 6 = Saturday).");
        RuleForEach(x => x.OpeningHours).SetValidator(new OpeningHourValidator());
    }
}

/// <summary>Open 0–23; close up to 30 (after midnight: 26 = 2 AM); at most 24 hours a day. Closed days are not checked.</summary>
public sealed class OpeningHourValidator : AbstractValidator<OpeningHourDto>
{
    public OpeningHourValidator()
    {
        When(x => !x.IsClosed, () =>
        {
            RuleFor(x => x.OpenHour).InclusiveBetween(0, 23);
            RuleFor(x => x.CloseHour).InclusiveBetween(1, 30)
                .GreaterThan(x => x.OpenHour).WithMessage("Closing must be after opening.")
                .Must((d, close) => close - d.OpenHour <= 24).WithMessage("At most 24 hours a day.");
        });
    }
}

public sealed class ClosureRequestValidator : AbstractValidator<ClosureRequest>
{
    public ClosureRequestValidator()
    {
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate).WithMessage("The end date can’t be before the start date.")
            .Must((x, to) => to.DayNumber - x.FromDate.DayNumber <= 366).WithMessage("Block at most one year at a time.");
        RuleFor(x => x.FromHour).NotNull().When(x => x.ToHour is not null).WithMessage("Choose both hours, or none for whole days.")
            .InclusiveBetween(0, 23).When(x => x.FromHour is not null);
        RuleFor(x => x.ToHour).NotNull().When(x => x.FromHour is not null).WithMessage("Choose both hours, or none for whole days.")
            .InclusiveBetween(1, 24).When(x => x.ToHour is not null)
            .GreaterThan(x => x.FromHour).When(x => x.ToHour is not null && x.FromHour is not null).WithMessage("The end hour must be after the start hour.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}
