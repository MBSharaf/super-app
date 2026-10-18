namespace SuperApp.Application.Features.Fantasy;

/* ---------- Public: competitions, clubs, players, fixtures ---------- */

/// <summary>A competition with its current season. <c>Gameweek</c> is the next gameweek open for changes (null when the season is over).</summary>
public sealed record CompetitionDto(string Key, string NameEn, string NameAr, string Country, int? Season, int? Gameweek, DateTime? Deadline,
    int? LastFinishedGameweek, int TotalGameweeks, decimal SquadBudget, int MaxPerClub, DateTime? LastSyncedAt);

/// <summary>A club. No logos for now (FR-FAN-02): kit colours instead; <c>LogoUrl</c> stays empty until logos are licensed.</summary>
public sealed record ClubDto(long Id, string Name, string NameAr, string ShortName, string KitColor, string KitColor2, string? LogoUrl);

/// <summary>A player with his season numbers. Position GK / DEF / MID / FWD; status Available / Doubtful / Injured / LeftTheClub.</summary>
public sealed record PlayerDto(long Id, string Name, string? NameAr, long ClubId, string Club, string Position, decimal Price, string Status,
    decimal SelectedBy, int TotalPoints, decimal Form, int LastPoints, int Goals, int Assists, int CleanSheets, int Minutes, int Bonus,
    int YellowCards, int Saves);

/// <summary>GET /fantasy/{comp}/players — sort: total (default), price, form, selected, goals, value.</summary>
public sealed record PlayersQuery(string? Position = null, long? Club = null, string? Q = null, string? Sort = null, decimal? MaxPrice = null,
    int Page = 1, int PageSize = 50);

public sealed record GameweekDto(int Number, DateTime Deadline, string Status, DateTime? ScoredAt);

/// <summary>Everything the fantasy pages need at once (players are cached data, never read live from the provider).</summary>
public sealed record BootstrapDto(CompetitionDto Competition, IReadOnlyList<ClubDto> Clubs, IReadOnlyList<PlayerDto> Players, IReadOnlyList<GameweekDto> Gameweeks);

/// <summary>A match. Status Scheduled / Live / Finished / Postponed / Cancelled; <c>Minute</c> while live.</summary>
public sealed record FixtureDto(long Id, int Gameweek, long HomeClubId, long AwayClubId, string HomeClub, string AwayClub, DateTime KickoffAt,
    string Status, int? HomeGoals, int? AwayGoals, int? Minute);

/// <summary>A player's gameweek: minutes, goals, assists, points (two matches in a double gameweek are added).</summary>
public sealed record PlayerGameweekDto(int Gameweek, int Minutes, int Goals, int Assists, int CleanSheets, int Saves, int Bonus, int YellowCards, bool RedCard, int Points);

public sealed record PlayerDetailDto(PlayerDto Player, IReadOnlyList<PlayerGameweekDto> History, IReadOnlyList<FixtureDto> Upcoming);

/// <summary>One row of the real league table, from finished matches.</summary>
public sealed record TableRowDto(int Position, long ClubId, string Club, int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst, int Points, IReadOnlyList<string> Form);

/* ---------- Customer: my team ---------- */

public sealed record PickDto(long PlayerId, int Slot, bool IsCaptain, bool IsViceCaptain, int Multiplier, int Points);

/// <summary>
/// My squad for the gameweek open for changes. <c>ActiveChip</c> is the chip played for that gameweek;
/// <c>TransfersCost</c> the points it will cost so far.
/// </summary>
public sealed record TeamDto(long Id, string Name, string Competition, int? Gameweek, DateTime? Deadline, decimal Bank, decimal SquadValue,
    int FreeTransfers, int TransfersCost, int TotalPoints, int? OverallRank, IReadOnlyList<string> ChipsUsed, string? ActiveChip, IReadOnlyList<PickDto> Picks);

/// <summary>POST /fantasy/{comp}/team — 15 player ids; the starting lineup can be sent too (else a default one is made).</summary>
public sealed record CreateTeamRequest(string Name, IReadOnlyList<long> Picks, LineupRequest? Lineup);

/// <summary>PUT /fantasy/{comp}/team/lineup — 11 starters, the bench in order (substitute goalkeeper first), captain and vice.</summary>
public sealed record LineupRequest(IReadOnlyList<long> Starters, IReadOnlyList<long> Bench, long CaptainId, long ViceCaptainId);

public sealed record RenameTeamRequest(string Name);

public sealed record TransferInput(long Out, long In);

/// <summary>POST /fantasy/{comp}/team/transfers — free transfers first, then 4 points each (none with a wildcard).</summary>
public sealed record TransfersRequest(IReadOnlyList<TransferInput> Transfers);

public sealed record TransfersResultDto(int Made, int Hits, TeamDto Team);

/// <summary>A scored gameweek of my team, with the picks as they counted.</summary>
public sealed record TeamGameweekDto(int Gameweek, int Points, int TransfersCost, int? Rank, string? Chip, int TotalAfter, IReadOnlyList<PickDto> Picks);

public sealed record TeamHistoryDto(int TotalPoints, int? OverallRank, int Managers, IReadOnlyList<TeamGameweekDto> Gameweeks);
