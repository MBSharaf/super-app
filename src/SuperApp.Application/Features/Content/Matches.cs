using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Content;

/* ---------- Matches (real football, from the fantasy cache) ---------- */

public sealed record MatchClubDto(long Id, string Name, string ShortName, string Kit, string Kit2, string? Logo = null);

/// <summary>
/// A real match of a followed competition. Status Scheduled / Live / Finished / Postponed / Cancelled. <c>Gameweek</c> is
/// the round; cups use 32 playoff, 16 last 16, 125 quarter-final, 150 semi-final, 200 final. <c>Clock</c> is the live
/// clock text while the match is on ("57'", "45+2'", "HT"), from the live feed.
/// </summary>
public sealed record MatchDto(long Id, string Competition, int Gameweek, MatchClubDto Home, MatchClubDto Away, DateTime KickoffAt, string Status,
    int? HomeGoals, int? AwayGoals, int? Minute, string? Venue = null, string? Clock = null);

/// <summary>A competition shown on the matches page (with its colour), and whether it has fantasy.</summary>
public sealed record MatchCompetitionDto(string Key, string Name, string NameAr, string ShortName, string Region, string Color, bool Fantasy);

/// <summary>A player's line in a match (from the provider's statistics).</summary>
public sealed record MatchPlayerDto(long PlayerId, long ClubId, string Name, string Position, int Minutes, int Goals, int Assists, int YellowCards, bool RedCard, int Points);

public sealed record MatchDetailDto(MatchDto Match, IReadOnlyList<MatchPlayerDto> Players);

public interface IMatchesReader
{
    Task<IReadOnlyList<MatchDto>> BetweenAsync(DateTime fromUtc, DateTime toUtc, string? competition, CancellationToken ct);

    Task<MatchDetailDto?> DetailAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<MatchCompetitionDto>> CompetitionsAsync(CancellationToken ct);
}

/// <summary>Matches for the site (API §6.2): fixtures and results of every followed competition, from the cache the sync job refreshes.</summary>
public interface IMatchesService
{
    /// <summary>Matches of <paramref name="days"/> Cairo days (1–14) from <paramref name="date"/> (default today).</summary>
    Task<IReadOnlyList<MatchDto>> MatchesAsync(DateOnly? date, int days, string? competition, CancellationToken ct);

    Task<IReadOnlyList<MatchCompetitionDto>> CompetitionsAsync(CancellationToken ct);

    Task<MatchDetailDto> MatchAsync(long id, CancellationToken ct);
}

public sealed class MatchesService(IMatchesReader matches, IClock clock) : IMatchesService
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>The Cairo calendar days (default today, 1–14 days) as a UTC range.</summary>
    public Task<IReadOnlyList<MatchDto>> MatchesAsync(DateOnly? date, int days, string? competition, CancellationToken ct)
    {
        if (days is < 1 or > 14)
        {
            throw new BadRequestException("Ask for 1 to 14 days.", "days");
        }
        var day = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, Cairo));
        var from = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Cairo);
        var to = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(days).ToDateTime(TimeOnly.MinValue), Cairo);
        return matches.BetweenAsync(from, to, competition, ct);
    }

    public Task<IReadOnlyList<MatchCompetitionDto>> CompetitionsAsync(CancellationToken ct) => matches.CompetitionsAsync(ct);

    public async Task<MatchDetailDto> MatchAsync(long id, CancellationToken ct) =>
        await matches.DetailAsync(id, ct) ?? throw new NotFoundException("Match not found.");
}
