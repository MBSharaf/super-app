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

public sealed record MatchClubDto(long Id, string Name, string ShortName, string Kit, string Kit2);

/// <summary>
/// A real match of a followed competition. Status Scheduled / Live / Finished / Postponed / Cancelled. <c>Gameweek</c> is
/// the round; cups use 32 playoff, 16 last 16, 125 quarter-final, 150 semi-final, 200 final.
/// </summary>
public sealed record MatchDto(long Id, string Competition, int Gameweek, MatchClubDto Home, MatchClubDto Away, DateTime KickoffAt, string Status,
    int? HomeGoals, int? AwayGoals, int? Minute, string? Venue = null);

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
