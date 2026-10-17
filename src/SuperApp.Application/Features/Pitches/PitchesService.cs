using FluentValidation;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Application.Features.Pitches;

/// <summary>Database queries for the public pitch pages (implemented in Infrastructure).</summary>
public interface IPitchesReader
{
    Task<PagedResult<PitchCardDto>> SearchAsync(PitchSearchQuery query, CancellationToken ct);

    /// <summary>A live pitch by its slug; null when it does not exist or is not live.</summary>
    Task<PitchDetailsDto?> DetailsAsync(string slug, CancellationToken ct);
}

/// <summary>Public pitches (API §3.1): search and the pitch page. Only live pitches are shown.</summary>
public interface IPitchesService
{
    Task<PagedResult<PitchCardDto>> SearchAsync(PitchSearchQuery query, CancellationToken ct);

    Task<PitchDetailsDto> GetAsync(string slug, CancellationToken ct);
}

public sealed class PitchesService(IPitchesReader reader, IValidator<PitchSearchQuery> rules) : IPitchesService
{
    public async Task<PagedResult<PitchCardDto>> SearchAsync(PitchSearchQuery query, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(query, ct);
        return await reader.SearchAsync(query with { Q = query.Q?.Trim() }, ct);
    }

    public async Task<PitchDetailsDto> GetAsync(string slug, CancellationToken ct) =>
        await reader.DetailsAsync(slug, ct) ?? throw new NotFoundException("This pitch does not exist or is not open for booking.");
}

public sealed class PitchSearchQueryValidator : AbstractValidator<PitchSearchQuery>
{
    private static readonly string[] Sorts = ["rating", "price", "-price", "distance", "name"];

    public PitchSearchQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Format).Must(f => f is null or 5 or 7 or 11).WithMessage("Format is 5, 7 or 11.");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice is not null);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice ?? 0).When(x => x.MaxPrice is not null).WithMessage("The highest price must be above the lowest.");
        RuleFor(x => x.Facilities).MaximumLength(200);
        RuleFor(x => x.Lat).InclusiveBetween(22, 32).When(x => x.Lat is not null);
        RuleFor(x => x.Lng).InclusiveBetween(24, 37).When(x => x.Lng is not null);
        RuleFor(x => x.Sort).Must(s => s is null || Sorts.Contains(s)).WithMessage("Sort by rating, price, -price, distance or name.");
        RuleFor(x => x).Must(x => x.Sort != "distance" || (x.Lat is not null && x.Lng is not null)).WithName("sort").WithMessage("Sorting by distance needs your location.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
