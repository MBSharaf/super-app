using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Application.Features.Tournaments;

/// <summary>A team as the site shows it. Phone only for the organiser; <c>Mine</c> = the signed-in captain's team.</summary>
public sealed record TournamentTeamDto(long Id, string Name, string Captain, string? Phone, bool Paid, bool Mine, string Status);

/// <summary>One match: its pitch hour comes from the booking that reserves it. Teams are null until they are known.</summary>
public sealed record TournamentMatchDto(long Id, int Round, int Slot, DateOnly Date, int Hour, long? HomeTeamId, long? AwayTeamId,
    int? HomeGoals, int? AwayGoals, long? PensWinnerId, long? WinnerTeamId, bool Played);

public sealed record StandingDto(long TeamId, string Name, int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst, int Points);

/// <summary>
/// Type: knockout | league. Status: open | full | running | finished | cancelled. Registration closes
/// <see cref="TournamentRules.RegistrationClosesDaysBefore"/> days before the first match day.
/// </summary>
public sealed record TournamentDto(long Id, string Name, string Type, long PitchId, string PitchSlug, string PitchName, string Organiser, int TeamCount,
    decimal EntryFee, string? PrizeText, DateOnly StartsOn, DateOnly EndsOn, int Hour, string? RulesText, string Status, DateOnly RegistrationClosesOn,
    int SpotsLeft, long? ChampionTeamId, IReadOnlyList<TournamentTeamDto> Teams, IReadOnlyList<TournamentMatchDto> Matches, IReadOnlyList<StandingDto> Table,
    string? PaymentUrl = null);

/// <summary>POST /partner/tournaments. Type knockout (4, 8 or 16 teams) or league (3–10); matches start at <c>Hour</c> on each match day, one hour each.</summary>
public sealed record CreateTournamentRequest(long PitchId, string Name, string Type, int TeamCount, decimal EntryFee, string? PrizeText,
    DateOnly StartsOn, int Hour, string RulesText);

/// <summary>POST /tournaments/{id}/teams — the signed-in person is the captain; pays the entry fee.</summary>
public sealed record RegisterTeamRequest(string TeamName, string Phone, bool AcceptWaiver);

/// <summary>POST /partner/tournaments/{id}/teams — a team the venue adds (pays cash at the venue).</summary>
public sealed record VenueTeamRequest(string TeamName, string? Phone);

/// <summary>PUT /partner/tournaments/{id}/matches/{matchId} — a knockout draw needs <c>PensWinnerId</c>.</summary>
public sealed record MatchResultRequest(int HomeGoals, int AwayGoals, long? PensWinnerId);

/// <summary>Tournament tables (with teams, matches and their bookings).</summary>
public interface ITournamentStore
{
    Task<IReadOnlyList<Tournament>> ListAsync(bool includeFinished, CancellationToken ct);

    Task<IReadOnlyList<Tournament>> OfPartnerAsync(long partnerId, CancellationToken ct);

    Task<IReadOnlyList<Tournament>> AllAsync(CancellationToken ct);

    Task<Tournament?> FindAsync(long id, CancellationToken ct);

    /// <summary>The tournament whose team paid with this payment.</summary>
    Task<Tournament?> ByTeamPaymentAsync(long paymentId, CancellationToken ct);

    void Add(Tournament tournament);

    void Remove(TournamentMatch match);

    /// <summary>Captain names by user id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct);

    Task<decimal?> PartnerCommissionAsync(long partnerId, CancellationToken ct);
}
