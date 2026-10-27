using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Football;

/// <summary>A competition as the admin sees it: its source, whether it is shown and has fantasy, and how much is cached.</summary>
public sealed record AdminCompetitionDto(string Key, string NameEn, string NameAr, string ShortName, string Region, string Color, string Provider,
    int ExternalLeagueId, bool FantasyEnabled, bool IsActive, int SortOrder, int Fixtures, DateTime? LastSyncedAt);

/// <summary>POST /admin/football/competitions — a competition from TheSportsDB (its league number), fixtures only.</summary>
public sealed record AddCompetitionRequest(string Key, string NameEn, string NameAr, string ShortName, string Region, string Color, int TheSportsDbId);

/// <summary>PUT /admin/football/competitions/{key} — names, place on the matches page, shown or hidden.</summary>
public sealed record UpdateCompetitionRequest(string NameEn, string NameAr, string ShortName, string Region, string Color, bool IsActive, int SortOrder);

public interface ICompetitionAdminRepository
{
    Task<IReadOnlyList<AdminCompetitionDto>> ListAsync(CancellationToken ct);

    Task<Competition?> FindAsync(string key, CancellationToken ct);

    Task<bool> TakenAsync(string key, string provider, int externalLeagueId, CancellationToken ct);

    Task<int> NextSortOrderAsync(CancellationToken ct);

    void Add(Competition competition);
}

/// <summary>The regions the matches page filters by.</summary>
public static class CompetitionRegions
{
    public static readonly string[] All = ["Egypt", "Africa", "Europe", "National teams"];
}

/// <summary>
/// The competitions the site follows (permission fantasy.manage). New ones come from TheSportsDB (fixtures and results,
/// no fantasy); the league number is checked with TheSportsDB first. The next scheduler run reads their matches.
/// </summary>
public interface ICompetitionsAdminService
{
    Task<IReadOnlyList<AdminCompetitionDto>> ListAsync(CancellationToken ct);

    Task<AdminCompetitionDto> AddAsync(AddCompetitionRequest request, CancellationToken ct);

    Task<AdminCompetitionDto> UpdateAsync(string key, UpdateCompetitionRequest request, CancellationToken ct);
}

public sealed class CompetitionsAdminService(
    ICompetitionAdminRepository competitions,
    IFootballSources sources,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IValidator<AddCompetitionRequest> addRules,
    IValidator<UpdateCompetitionRequest> updateRules) : ICompetitionsAdminService
{
    private const string TheSportsDb = "thesportsdb";

    public Task<IReadOnlyList<AdminCompetitionDto>> ListAsync(CancellationToken ct) => competitions.ListAsync(ct);

    public async Task<AdminCompetitionDto> AddAsync(AddCompetitionRequest request, CancellationToken ct)
    {
        await addRules.ValidateAndThrowAsync(request, ct);
        var key = request.Key.Trim().ToLowerInvariant();
        if (await competitions.TakenAsync(key, TheSportsDb, request.TheSportsDbId, ct))
        {
            throw new ConflictException("This key or TheSportsDB league is already followed.", "competition_exists");
        }
        await CheckLeagueAsync(key, request, ct);
        var row = new Competition
        {
            Key = key, NameEn = request.NameEn.Trim(), NameAr = request.NameAr.Trim(), ShortName = request.ShortName.Trim(), Region = request.Region,
            Color = request.Color.ToUpperInvariant(), Country = request.Region, Provider = TheSportsDb, ExternalLeagueId = request.TheSportsDbId,
            FantasyEnabled = false, SortOrder = await competitions.NextSortOrderAsync(ct),
        };
        competitions.Add(row);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "football.competition_added", $"{row.NameEn} (TheSportsDB {row.ExternalLeagueId})", "Competition", key, ct: ct);
        return (await competitions.ListAsync(ct)).Single(c => c.Key == key);
    }

    public async Task<AdminCompetitionDto> UpdateAsync(string key, UpdateCompetitionRequest request, CancellationToken ct)
    {
        await updateRules.ValidateAndThrowAsync(request, ct);
        var row = await competitions.FindAsync(key.ToLowerInvariant(), ct) ?? throw new NotFoundException("Competition not found.");
        (row.NameEn, row.NameAr, row.ShortName, row.Region) = (request.NameEn.Trim(), request.NameAr.Trim(), request.ShortName.Trim(), request.Region);
        (row.Color, row.IsActive, row.SortOrder) = (request.Color.ToUpperInvariant(), request.IsActive, request.SortOrder);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "football.competition_updated", $"{row.NameEn}: {(row.IsActive ? "shown" : "hidden")}, place {row.SortOrder}",
            "Competition", row.Key, ct: ct);
        return (await competitions.ListAsync(ct)).Single(c => c.Key == row.Key);
    }

    /// <summary>Asks TheSportsDB for the league's current season (skipped offline); an unknown number → 422 unknown_league.</summary>
    private async Task CheckLeagueAsync(string key, AddCompetitionRequest request, CancellationToken ct)
    {
        var source = sources.For(new CompetitionRef(0, key, request.TheSportsDbId, request.NameEn, TheSportsDb, false));
        if (source is null)
        {
            return;
        }
        try
        {
            await source.CurrentSeasonAsync(request.TheSportsDbId, ct);
        }
        catch (HttpRequestException)
        {
            throw new BusinessRuleException("TheSportsDB has no current season for that league number. Check it on thesportsdb.com.", "unknown_league");
        }
    }
}

public sealed class AddCompetitionRequestValidator : AbstractValidator<AddCompetitionRequest>
{
    public AddCompetitionRequestValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches("^[A-Za-z0-9]{2,10}$").WithMessage("2–10 letters or digits, e.g. saudi.");
        RuleFor(x => x.TheSportsDbId).InclusiveBetween(1, 999_999).WithMessage("The league number from thesportsdb.com.");
        CompetitionRules.Shared(this, x => x.NameEn, x => x.NameAr, x => x.ShortName, x => x.Region, x => x.Color);
    }
}

public sealed class UpdateCompetitionRequestValidator : AbstractValidator<UpdateCompetitionRequest>
{
    public UpdateCompetitionRequestValidator()
    {
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 999);
        CompetitionRules.Shared(this, x => x.NameEn, x => x.NameAr, x => x.ShortName, x => x.Region, x => x.Color);
    }
}

internal static class CompetitionRules
{
    public static void Shared<T>(AbstractValidator<T> v, System.Linq.Expressions.Expression<Func<T, string>> nameEn, System.Linq.Expressions.Expression<Func<T, string>> nameAr,
        System.Linq.Expressions.Expression<Func<T, string>> shortName, System.Linq.Expressions.Expression<Func<T, string>> region, System.Linq.Expressions.Expression<Func<T, string>> color)
    {
        v.RuleFor(nameEn).NotEmpty().Length(2, 80);
        v.RuleFor(nameAr).NotEmpty().Length(2, 80);
        v.RuleFor(shortName).NotEmpty().MaximumLength(30);
        v.RuleFor(region).Must(r => CompetitionRegions.All.Contains(r)).WithMessage($"One of: {string.Join(", ", CompetitionRegions.All)}.");
        v.RuleFor(color).NotEmpty().Matches("^#[0-9A-Fa-f]{6}$").WithMessage("A colour such as #0B6E4F.");
    }
}
