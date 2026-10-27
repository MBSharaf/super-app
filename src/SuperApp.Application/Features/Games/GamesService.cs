using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Games;

public sealed record GamesQuery(int? AreaId = null, DateOnly? Date = null, string? Level = null);

/// <summary>An open game card. <c>Joined</c> counts accepted players; <c>Level</c>: Any, Casual, Intermediate, Competitive.</summary>
public sealed record GameDto(string Code, string? Title, string PitchSlug, string PitchName, string AreaEn, int AreaId, DateOnly Date, int StartHour, int Duration,
    string Level, string? Positions, int PlayersNeeded, int Joined, decimal SharePrice, string Status, bool RequestsClosed, string HostName, Guid HostId, string? Notes = null);

/// <summary>A request to join. Status: Waiting, Accepted, Declined, Withdrawn, Removed, Waitlisted.</summary>
public sealed record GameRequestDto(long Id, Guid PlayerId, string PlayerName, string? Position, string? Message, string Status, DateTime CreatedAt,
    int Games = 0, int ShowedUp = 0, decimal? AverageStars = null);

/// <summary>The game page: the host also sees every request; players see who is in.</summary>
/// <summary>The game page. The host also gets every request, the auto-accept setting and the booking code.</summary>
public sealed record GameDetailDto(GameDto Game, IReadOnlyList<GameRequestDto> Players, IReadOnlyList<GameRequestDto>? Requests, string? MyStatus, string? PaymentUrl = null,
    bool? AutoAccept = null, string? BookingCode = null);

/// <summary>GET /games/mine — the games I post (not cancelled) and the ones I asked to join, from yesterday on.</summary>
public sealed record MyGamesDto(IReadOnlyList<GameDto> Hosting, IReadOnlyList<MyGameDto> Joined);

public sealed record MyGameDto(GameDto Game, string MyStatus);

public sealed record PostGameRequest(string BookingCode, int PlayersNeeded, string Level, string? Positions, string? Title, decimal SharePrice, bool AutoAccept, string? Notes = null);

public sealed record EditGameRequest(int PlayersNeeded, string Level, string? Positions, string? Title, bool RequestsClosed, bool? AutoAccept = null);

public sealed record JoinGameRequest(string? Position, string? Message, bool PayFromWallet = false);

public sealed record RatingInput(Guid PlayerId, int? Stars, bool ShowedUp);

public sealed record RatingsRequest(IReadOnlyList<RatingInput> Ratings);

/// <summary>A player's record: games played, how often they showed up, their average stars.</summary>
public sealed record PlayerRecordDto(Guid PlayerId, string Name, int Games, int ShowedUp, decimal? AverageStars);

public interface IGameRepository
{
    Task<IReadOnlyList<GameDto>> OpenAsync(GamesQuery query, DateOnly today, CancellationToken ct);

    Task<GameDto?> CardAsync(string code, CancellationToken ct);

    /// <summary>The game with its booking and requests, tracked for changes.</summary>
    Task<Game?> FindAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<GameRequestDto>> RequestsAsync(long gameId, CancellationToken ct);

    Task<Booking?> BookingAsync(string code, CancellationToken ct);

    Task<bool> ExistsForBookingAsync(long bookingId, CancellationToken ct);

    Task<string> NewCodeAsync(CancellationToken ct);

    void Add(Game game);

    void AddRequest(GameRequest request);

    /// <summary>The join request paid with this payment (with its game and booking).</summary>
    Task<GameRequest?> RequestByPaymentAsync(long paymentId, CancellationToken ct);

    Task<List<PlayerRating>> RatingsAsync(long gameId, CancellationToken ct);

    void AddRating(PlayerRating rating);

    Task<PlayerRecordDto?> RecordAsync(Guid playerId, CancellationToken ct);

    Task<MyGamesDto> MineAsync(Guid me, DateOnly from, CancellationToken ct);
}

/// <summary>
/// Find players (API §3.8): a player posts one of their bookings as a game, others ask to join and pay their share (held
/// for the host until kick-off, then the host's to spend; refunded to the player's wallet when declined, removed or
/// withdrawn), the host accepts or declines, and rates who showed up after kick-off.
/// </summary>
public interface IGamesService
{
    Task<IReadOnlyList<GameDto>> OpenAsync(GamesQuery query, CancellationToken ct);

    Task<GameDetailDto> GetAsync(string code, CancellationToken ct);

    Task<GameDetailDto> PostAsync(PostGameRequest request, CancellationToken ct);

    Task<GameDetailDto> EditAsync(string code, EditGameRequest request, CancellationToken ct);

    Task<GameDetailDto> JoinAsync(string code, JoinGameRequest request, CancellationToken ct);

    Task<GameDetailDto> DecideAsync(string code, long requestId, string action, CancellationToken ct);

    Task<GameDetailDto> WithdrawAsync(string code, CancellationToken ct);

    Task RateAsync(string code, RatingsRequest request, CancellationToken ct);

    Task<PlayerRecordDto> RecordAsync(Guid playerId, CancellationToken ct);

    Task<MyGamesDto> MineAsync(CancellationToken ct);

    /// <summary>The host cancels before kick-off: every player still in or waiting gets their share back and is told.</summary>
    Task<GameDetailDto> CancelAsync(string code, CancellationToken ct);
}

public sealed class GamesService(
    IGameRepository games,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IFeatureFlags flags,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<PostGameRequest> postRules,
    IValidator<EditGameRequest> editRules,
    IValidator<RatingsRequest> ratingRules,
    Payments.IPaymentsService payments,
    Money.ILedger ledger) : IGamesService
{
    public Task<IReadOnlyList<GameDto>> OpenAsync(GamesQuery query, CancellationToken ct) => games.OpenAsync(query, Today(), ct);

    public async Task<GameDetailDto> GetAsync(string code, CancellationToken ct)
    {
        var card = await games.CardAsync(code, ct) ?? throw new NotFoundException("This game does not exist.");
        var game = (await games.FindAsync(code, ct))!;
        var all = await games.RequestsAsync(game.Id, ct);
        var me = currentUser.UserId;
        var host = me == game.HostId;
        return new GameDetailDto(card, all.Where(r => r.Status == nameof(GameRequestStatus.Accepted)).ToList(), host ? all : null,
            all.FirstOrDefault(r => r.PlayerId == me)?.Status, null, host ? game.AutoAccept : null, host ? game.Booking.Code : null);
    }

    public async Task<GameDetailDto> PostAsync(PostGameRequest request, CancellationToken ct)
    {
        await postRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync("kill.games", ct);
        var me = Me();
        var booking = await games.BookingAsync(request.BookingCode, ct);
        if (booking is null || booking.CustomerId != me)
        {
            throw new BusinessRuleException("Post a game for one of your own bookings.", "booking_not_mine");
        }
        if (booking.Status != BookingStatus.Confirmed || Start(booking) <= CairoTime.Now(clock))
        {
            throw new BusinessRuleException("This booking has already started or is no longer active.", "booking_past");
        }
        if (await games.ExistsForBookingAsync(booking.Id, ct))
        {
            throw new ConflictException("This booking already has a game.", "game_exists");
        }
        var game = new Game
        {
            Code = await games.NewCodeAsync(ct), HostId = me, BookingId = booking.Id, Title = request.Title?.Trim(), PlayersNeeded = (byte)request.PlayersNeeded,
            Positions = request.Positions?.Trim(), Level = Enum.Parse<GameLevel>(request.Level), SharePrice = request.SharePrice, AutoAccept = request.AutoAccept, Status = GameStatus.Open,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };
        games.Add(game);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(game.Code, ct);
    }

    public async Task<GameDetailDto> EditAsync(string code, EditGameRequest request, CancellationToken ct)
    {
        await editRules.ValidateAndThrowAsync(request, ct);
        var game = await HostGameAsync(code, ct);
        var accepted = game.Requests.Count(r => r.Status == GameRequestStatus.Accepted);
        if (request.PlayersNeeded < accepted)
        {
            throw new BusinessRuleException($"{accepted} players are already in. Remove some first.", "below_joined");
        }
        (game.PlayersNeeded, game.Level, game.Positions, game.Title, game.RequestsClosed) =
            ((byte)request.PlayersNeeded, Enum.Parse<GameLevel>(request.Level), request.Positions?.Trim(), request.Title?.Trim(), request.RequestsClosed);
        game.AutoAccept = request.AutoAccept ?? game.AutoAccept;
        game.Status = accepted >= game.PlayersNeeded ? GameStatus.Full : GameStatus.Open;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(code, ct);
    }

    public async Task<GameDetailDto> JoinAsync(string code, JoinGameRequest request, CancellationToken ct)
    {
        var me = Me();
        var game = await games.FindAsync(code, ct) ?? throw new NotFoundException("This game does not exist.");
        if (game.HostId == me) { throw new BusinessRuleException("This is your own game.", "own_game"); }
        if (game.Status is GameStatus.Played or GameStatus.Cancelled || game.RequestsClosed || Start(game.Booking) <= CairoTime.Now(clock))
        {
            throw new BusinessRuleException("This game is no longer taking players.", "closed");
        }
        if (game.Requests.Any(r => r.PlayerId == me))
        {
            throw new ConflictException("You already asked to join this game.", "already_asked");
        }
        var full = game.Requests.Count(r => r.Status == GameRequestStatus.Accepted) >= game.PlayersNeeded;
        var status = full ? GameRequestStatus.Waitlisted : game.AutoAccept ? GameRequestStatus.Accepted : GameRequestStatus.Waiting;
        var row = new GameRequest { GameId = game.Id, PlayerId = me, Position = request.Position?.Trim(), Message = request.Message?.Trim(), Status = status };
        games.AddRequest(row);
        var url = await PayShareAsync(row, game, me, code, request.PayFromWallet, ct);
        Refill(game, status == GameRequestStatus.Accepted ? 1 : 0);
        await unitOfWork.SaveChangesAsync(ct);
        if (url is null && row.Payment is { } paid)
        {
            await ledger.HeldShareAsync(paid, game.HostId, "GameRequest", row.Id, CairoTime.ToUtc(Start(game.Booking)), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        await Tell(game.HostId, status == GameRequestStatus.Accepted ? "A player joined your game" : "A player asked to join your game", game, ct);
        return await GetAsync(code, ct) with { PaymentUrl = url };
    }

    /// <summary>The player's share is paid when asking to join (simulated: at once; Paymob: its page).</summary>
    private async Task<string?> PayShareAsync(GameRequest row, Game game, Guid me, string code, bool fromWallet, CancellationToken ct)
    {
        if (game.SharePrice <= 0)
        {
            return null;
        }
        var started = await payments.StartAsync(new Payments.PaymentRequest(PaymentPurpose.GameShare, game.SharePrice, me, $"Game {code} share", $"#/players/{code}", fromWallet), ct);
        row.Payment = started.Payment;
        return started.RedirectUrl;
    }

    /// <summary>A paid share goes back to the player's wallet.</summary>
    private async Task RefundShareAsync(GameRequest row, Guid by, string reason, CancellationToken ct)
    {
        if (row.Payment is { Status: PaymentStatus.Paid } payment)
        {
            await ledger.ReverseAsync(payment, row.PlayerId, "GameRequest", row.Id, by, reason, ct);
        }
    }

    public async Task<GameDetailDto> DecideAsync(string code, long requestId, string action, CancellationToken ct)
    {
        var game = await HostGameAsync(code, ct);
        var request = game.Requests.FirstOrDefault(r => r.Id == requestId) ?? throw new NotFoundException("Request not found.");
        var accepted = game.Requests.Count(r => r.Status == GameRequestStatus.Accepted);
        (request.Status, var message) = action switch
        {
            "accept" when request.Status is GameRequestStatus.Waiting or GameRequestStatus.Waitlisted && accepted < game.PlayersNeeded => (GameRequestStatus.Accepted, "You’re in the game"),
            "accept" => throw new BusinessRuleException("The game is full, or this request was already decided.", "full"),
            "decline" when request.Status is GameRequestStatus.Waiting or GameRequestStatus.Waitlisted => (GameRequestStatus.Declined, "The host declined your request — your share is refunded"),
            "remove" when request.Status == GameRequestStatus.Accepted => (GameRequestStatus.Removed, "The host removed you from the game — your share is refunded"),
            _ => throw new BusinessRuleException("This request can't be changed that way.", "wrong_status"),
        };
        if (request.Status is GameRequestStatus.Declined or GameRequestStatus.Removed)
        {
            await RefundShareAsync(request, game.HostId, request.Status == GameRequestStatus.Declined ? "Declined by the host" : "Removed by the host", ct);
        }
        Refill(game, 0);
        await unitOfWork.SaveChangesAsync(ct);
        await Tell(request.PlayerId, message, game, ct);
        return await GetAsync(code, ct);
    }

    public async Task<GameDetailDto> WithdrawAsync(string code, CancellationToken ct)
    {
        var me = Me();
        var game = await games.FindAsync(code, ct) ?? throw new NotFoundException("This game does not exist.");
        var mine = game.Requests.FirstOrDefault(r => r.PlayerId == me && r.Status is GameRequestStatus.Waiting or GameRequestStatus.Accepted or GameRequestStatus.Waitlisted)
            ?? throw new NotFoundException("You haven’t asked to join this game.");
        if (Start(game.Booking) <= CairoTime.Now(clock)) { throw new BusinessRuleException("The game has started.", "started"); }
        mine.Status = GameRequestStatus.Withdrawn;
        await RefundShareAsync(mine, me, "Left the game", ct);
        Refill(game, 0);
        await unitOfWork.SaveChangesAsync(ct);
        await Tell(game.HostId, "A player left your game", game, ct);
        return await GetAsync(code, ct);
    }

    public async Task RateAsync(string code, RatingsRequest request, CancellationToken ct)
    {
        await ratingRules.ValidateAndThrowAsync(request, ct);
        var game = await HostGameAsync(code, ct);
        if (Start(game.Booking) > CairoTime.Now(clock)) { throw new BusinessRuleException("Rate the players after kick-off.", "not_started"); }
        var players = game.Requests.Where(r => r.Status == GameRequestStatus.Accepted).Select(r => r.PlayerId).ToHashSet();
        var existing = await games.RatingsAsync(game.Id, ct);
        foreach (var r in request.Ratings.Where(r => players.Contains(r.PlayerId)))
        {
            var row = existing.FirstOrDefault(x => x.PlayerId == r.PlayerId);
            if (row is null) { row = new PlayerRating { GameId = game.Id, RaterId = game.HostId, PlayerId = r.PlayerId }; games.AddRating(row); }
            (row.Stars, row.ShowedUp) = ((byte?)r.Stars, r.ShowedUp);
        }
        game.Status = GameStatus.Played;
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<MyGamesDto> MineAsync(CancellationToken ct) => games.MineAsync(Me(), Today().AddDays(-1), ct);

    public async Task<GameDetailDto> CancelAsync(string code, CancellationToken ct)
    {
        var game = await HostGameAsync(code, ct);
        if (game.Status is GameStatus.Cancelled or GameStatus.Played || Start(game.Booking) <= CairoTime.Now(clock))
        {
            throw new BusinessRuleException("This game has started or is already closed.", "closed");
        }
        var players = game.Requests.Where(r => r.Status is GameRequestStatus.Waiting or GameRequestStatus.Accepted or GameRequestStatus.Waitlisted).ToList();
        foreach (var r in players)
        {
            r.Status = GameRequestStatus.Removed;
            await RefundShareAsync(r, game.HostId, "The host cancelled the game", ct);
        }
        game.Status = GameStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var r in players)
        {
            await Tell(r.PlayerId, "The host cancelled the game — your share is refunded", game, ct);
        }
        return await GetAsync(code, ct);
    }

    public async Task<PlayerRecordDto> RecordAsync(Guid playerId, CancellationToken ct) =>
        await games.RecordAsync(playerId, ct) ?? throw new NotFoundException("Player not found.");

    /// <summary>Full when the accepted players fill it; a freed place goes to the first waitlisted player.</summary>
    private static void Refill(Game game, int justAccepted)
    {
        var accepted = game.Requests.Count(r => r.Status == GameRequestStatus.Accepted) + justAccepted;
        foreach (var next in game.Requests.Where(r => r.Status == GameRequestStatus.Waitlisted).OrderBy(r => r.CreatedAt).Take(Math.Max(0, game.PlayersNeeded - accepted)))
        {
            next.Status = GameRequestStatus.Waiting;
        }
        game.Status = accepted >= game.PlayersNeeded ? GameStatus.Full : GameStatus.Open;
    }

    private async Task<Game> HostGameAsync(string code, CancellationToken ct)
    {
        var game = await games.FindAsync(code, ct);
        return game is not null && game.HostId == Me() ? game : throw new NotFoundException("This game does not exist.");
    }

    private Task Tell(Guid user, string title, Game game, CancellationToken ct) =>
        notifier.NotifyUserAsync(user, new NotificationMessage(NotificationCategory.Bookings, title, game.Title ?? game.Code, $"#/players/{game.Code}"), ct);

    private static DateTime Start(Booking b) => b.Date.ToDateTime(new TimeOnly(b.StartHour, 0));

    private DateOnly Today() => DateOnly.FromDateTime(CairoTime.Now(clock));

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class PostGameRequestValidator : AbstractValidator<PostGameRequest>
{
    public PostGameRequestValidator()
    {
        RuleFor(x => x.BookingCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.PlayersNeeded).InclusiveBetween(1, 21);
        RuleFor(x => x.Level).IsEnumName(typeof(GameLevel), caseSensitive: true).WithMessage("Level is Any, Casual, Intermediate or Competitive.");
        RuleFor(x => x.SharePrice).InclusiveBetween(0, 5000);
        RuleFor(x => x.Notes).MaximumLength(280);
        RuleFor(x => x.Title).MaximumLength(100);
        RuleFor(x => x.Positions).MaximumLength(100);
    }
}

public sealed class EditGameRequestValidator : AbstractValidator<EditGameRequest>
{
    public EditGameRequestValidator()
    {
        RuleFor(x => x.PlayersNeeded).InclusiveBetween(1, 21);
        RuleFor(x => x.Level).IsEnumName(typeof(GameLevel), caseSensitive: true);
        RuleFor(x => x.Title).MaximumLength(100);
        RuleFor(x => x.Positions).MaximumLength(100);
    }
}

public sealed class RatingsRequestValidator : AbstractValidator<RatingsRequest>
{
    public RatingsRequestValidator()
    {
        RuleFor(x => x.Ratings).NotEmpty().Must(r => r.Count <= 21);
        RuleForEach(x => x.Ratings).ChildRules(r => r.RuleFor(x => x.Stars).InclusiveBetween(1, 5).When(x => x.Stars is not null));
    }
}

/// <summary>The gateway confirmed (or refused) a player's game share.</summary>
public sealed class GameSharePaymentListener(IGameRepository games, Money.ILedger ledger, IUnitOfWork unitOfWork) : Payments.IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.GameShare;

    public async Task PaidAsync(Domain.Entities.Money.Payment payment, CancellationToken ct)
    {
        if (await games.RequestByPaymentAsync(payment.Id, ct) is { } row && row.Status != GameRequestStatus.Withdrawn)
        {
            var b = row.Game.Booking;
            await ledger.HeldShareAsync(payment, row.Game.HostId, "GameRequest", row.Id, CairoTime.ToUtc(b.Date.ToDateTime(new TimeOnly(b.StartHour, 0))), ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task FailedAsync(Domain.Entities.Money.Payment payment, CancellationToken ct)
    {
        if (await games.RequestByPaymentAsync(payment.Id, ct) is { } row && row.Status != GameRequestStatus.Withdrawn)
        {
            row.Status = GameRequestStatus.Withdrawn;
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
