using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Games;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Games (one per booking), their join requests and the host's ratings.</summary>
public sealed class GameRepository(AppDbContext db) : IGameRepository
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<IReadOnlyList<GameDto>> OpenAsync(GamesQuery query, DateOnly today, CancellationToken ct)
    {
        var q = db.Set<Game>().Where(g => !g.HiddenByAdmin && (g.Status == GameStatus.Open || g.Status == GameStatus.Full) && g.Booking.Date >= today
            && g.Booking.Status == BookingStatus.Confirmed);
        if (query.AreaId is { } area) { q = q.Where(g => g.Booking.Pitch.Venue.AreaId == area); }
        if (query.Date is { } date) { q = q.Where(g => g.Booking.Date == date); }
        if (Enum.TryParse<GameLevel>(query.Level, out var level)) { q = q.Where(g => g.Level == level || g.Level == GameLevel.Any); }
        return await Cards(q.OrderBy(g => g.Booking.Date).ThenBy(g => g.Booking.StartHour).Take(100)).ToListAsync(ct);
    }

    public Task<GameDto?> CardAsync(string code, CancellationToken ct) => Cards(db.Set<Game>().Where(g => g.Code == code && !g.HiddenByAdmin)).FirstOrDefaultAsync(ct);

    public Task<Game?> FindAsync(string code, CancellationToken ct) =>
        db.Set<Game>().Include(g => g.Booking).Include(g => g.Requests).ThenInclude(r => r.Payment).FirstOrDefaultAsync(g => g.Code == code && !g.HiddenByAdmin, ct);

    public Task<GameRequest?> RequestByPaymentAsync(long paymentId, CancellationToken ct) =>
        db.Set<GameRequest>().Include(r => r.Payment).Include(r => r.Game).ThenInclude(g => g.Booking).FirstOrDefaultAsync(r => r.PaymentId == paymentId, ct);

    public async Task<IReadOnlyList<GameRequestDto>> RequestsAsync(long gameId, CancellationToken ct) =>
        await db.Set<GameRequest>().Where(r => r.GameId == gameId).OrderBy(r => r.CreatedAt)
            .Select(r => new GameRequestDto(r.Id, r.PlayerId, r.Player.FullName, r.Position, r.Message, r.Status.ToString(), r.CreatedAt,
                db.Set<PlayerRating>().Count(x => x.PlayerId == r.PlayerId), db.Set<PlayerRating>().Count(x => x.PlayerId == r.PlayerId && x.ShowedUp),
                db.Set<PlayerRating>().Where(x => x.PlayerId == r.PlayerId && x.Stars != null).Average(x => (decimal?)x.Stars))).ToListAsync(ct);

    public async Task<MyGamesDto> MineAsync(Guid me, DateOnly from, CancellationToken ct)
    {
        var hosting = await Cards(db.Set<Game>().Where(g => g.HostId == me && g.Status != GameStatus.Cancelled && g.Booking.Date >= from)
            .OrderBy(g => g.Booking.Date).ThenBy(g => g.Booking.StartHour)).ToListAsync(ct);
        var mine = await db.Set<GameRequest>().Where(r => r.PlayerId == me && r.Game.Booking.Date >= from
                && (r.Status == GameRequestStatus.Waiting || r.Status == GameRequestStatus.Accepted || r.Status == GameRequestStatus.Waitlisted))
            .Select(r => new { r.Game.Code, r.Status }).ToListAsync(ct);
        var codes = mine.Select(m => m.Code).ToList();
        var cards = await Cards(db.Set<Game>().Where(g => codes.Contains(g.Code))).ToListAsync(ct);
        var joined = cards.Select(c => new MyGameDto(c, mine.First(m => m.Code == c.Code).Status.ToString())).OrderBy(x => x.Game.Date).ThenBy(x => x.Game.StartHour).ToList();
        return new MyGamesDto(hosting, joined);
    }

    public Task<Booking?> BookingAsync(string code, CancellationToken ct) => db.Set<Booking>().FirstOrDefaultAsync(b => b.Code == code, ct);

    public Task<bool> ExistsForBookingAsync(long bookingId, CancellationToken ct) => db.Set<Game>().AnyAsync(g => g.BookingId == bookingId, ct);

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "GM-" + new string(Enumerable.Range(0, 6).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<Game>().AnyAsync(g => g.Code == code, ct))
            {
                return code;
            }
        }
    }

    public void Add(Game game) => db.Set<Game>().Add(game);

    public void AddRequest(GameRequest request) => db.Set<GameRequest>().Add(request);

    public Task<List<PlayerRating>> RatingsAsync(long gameId, CancellationToken ct) => db.Set<PlayerRating>().Where(r => r.GameId == gameId).ToListAsync(ct);

    public void AddRating(PlayerRating rating) => db.Set<PlayerRating>().Add(rating);

    public async Task<PlayerRecordDto?> RecordAsync(Guid playerId, CancellationToken ct)
    {
        var name = await db.Users.Where(u => u.Id == playerId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        if (name is null)
        {
            return null;
        }
        var ratings = db.Set<PlayerRating>().Where(r => r.PlayerId == playerId);
        var games = await ratings.CountAsync(ct);
        var showed = await ratings.CountAsync(r => r.ShowedUp, ct);
        var stars = await ratings.Where(r => r.Stars != null).Select(r => (double?)r.Stars).AverageAsync(ct);
        return new PlayerRecordDto(playerId, name, games, showed, stars is null ? null : Math.Round((decimal)stars.Value, 1));
    }

    private static IQueryable<GameDto> Cards(IQueryable<Game> q) => q.Select(g => new GameDto(
        g.Code, g.Title, g.Booking.Pitch.Slug, g.Booking.Pitch.Name, g.Booking.Pitch.Venue.Area.NameEn, g.Booking.Pitch.Venue.AreaId,
        g.Booking.Date, g.Booking.StartHour, g.Booking.Duration, g.Level.ToString(), g.Positions, g.PlayersNeeded,
        g.Requests.Count(r => r.Status == GameRequestStatus.Accepted), g.SharePrice, g.Status.ToString(), g.RequestsClosed, g.Host.FullName, g.HostId, g.Notes));
}
