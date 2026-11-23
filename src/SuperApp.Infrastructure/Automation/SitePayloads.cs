using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SuperApp.Application.Features.Webhooks;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Automation;

/// <summary>
/// Builds the demo site's JSON for an outbox event (js/services/automation.js), so the owner's n8n workflows read the same
/// fields from supermalaab.com as from the GitHub site: <c>{ event, source: "server", site, sentAt, ...details }</c>.
/// Returns null for events the demo site has no shape for, or when the record is gone.
/// </summary>
public sealed partial class SitePayloads(AppDbContext db, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Server event → the demo site's event name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>
    {
        [WebhookEvents.BookingCreated] = "booking.created", [WebhookEvents.BookingCancelled] = "booking.cancelled",
        [WebhookEvents.PackageCreated] = "package.created", [WebhookEvents.OrderCreated] = "order.created",
        [WebhookEvents.ReviewCreated] = "review.created", [WebhookEvents.SlotPosted] = "slot.posted",
        [WebhookEvents.PitchSubmitted] = "pitch.added", [WebhookEvents.PitchDeleted] = "pitch.removed",
        [WebhookEvents.CancellationRequested] = "cancellation.requested", [WebhookEvents.CancellationApproved] = "cancellation.approved",
        [WebhookEvents.CancellationRejected] = "cancellation.rejected",
    };

    public async Task<string?> BuildAsync(OutboxEvent e, CancellationToken ct)
    {
        if (!Names.TryGetValue(e.Type, out var name))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(e.PayloadJson);
        var root = doc.RootElement;
        var id = Prop(root, "entityId")?.GetString() ?? string.Empty;
        var data = Prop(root, "data");
        Dictionary<string, object?>? details = e.Type switch
        {
            WebhookEvents.BookingCreated or WebhookEvents.BookingCancelled => await BookingAsync(id, ct),
            WebhookEvents.PackageCreated => await PackageAsync(id, ct),
            WebhookEvents.OrderCreated => await OrderAsync(id, ct),
            WebhookEvents.ReviewCreated => await ReviewAsync(id, ct),
            WebhookEvents.SlotPosted => await SlotAsync(id, e.OccurredAt, ct),
            WebhookEvents.PitchSubmitted or WebhookEvents.PitchDeleted => await PitchEventAsync(id, e.Type == WebhookEvents.PitchDeleted, e.OccurredAt, ct),
            _ => await CancellationAsync(id, data, ct),
        };
        if (details is null)
        {
            return null;
        }
        var body = new Dictionary<string, object?> { ["event"] = name, ["source"] = "server", ["site"] = Link("/"), ["sentAt"] = DateTime.UtcNow };
        foreach (var (k, v) in details)
        {
            body[k] = v;
        }
        return JsonSerializer.Serialize(body, Json);
    }

    private static JsonElement? Prop(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var p in el.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind != JsonValueKind.Null)
            {
                return p.Value;
            }
        }
        return null;
    }

    /// <summary>A page on the real site (Site:PublicUrl, e.g. https://supermalaab.com/booking/BK-1).</summary>
    private string Link(string path)
    {
        var site = configuration["Site:PublicUrl"];
        var root = string.IsNullOrWhiteSpace(site) ? "https://localhost:7254/super-app/" : site.TrimEnd('/') + "/";
        return root + path.TrimStart('/');
    }

    private static string Hh(int hour, int minute = 0) => $"{hour % 24:00}:{minute:00}";

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Lower(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static object? Person(User? u) => u is null ? null : new { id = u.Id, name = u.FullName, email = u.Email, phone = u.PhoneNumber, city = (string?)null };

    private IQueryable<Pitch> Pitches(bool withDeleted = false) =>
        (withDeleted ? db.Set<Pitch>().IgnoreQueryFilters() : db.Set<Pitch>())
            .Include(p => p.Venue).ThenInclude(v => v.Area).ThenInclude(a => a.Governorate)
            .Include(p => p.Venue).ThenInclude(v => v.Partner).ThenInclude(pa => pa.User)
            .Include(p => p.CancellationPolicy);

    /// <summary>Like the demo's pitchInfo / pitchOf: the pitch and the partner who owns it.</summary>
    private object PitchInfo(Pitch p, string? status = null) => new
    {
        id = p.Slug, name = p.Name, sport = Lower(p.Sport), format = $"{p.Format}-a-side", players = p.Format * 2, surface = Lower(p.Surface), indoor = p.IsIndoor,
        area = p.Venue.Area?.NameEn, city = p.Venue.Area?.Governorate?.NameEn, address = p.Venue.Address, lat = p.Venue.Lat, lng = p.Venue.Lng,
        pricePerHour = p.PricePerHour, peakPricePerHour = p.PeakPricePerHour, cancelPolicy = p.CancellationPolicy?.Key, status = status ?? Lower(p.Status),
        rating = p.Rating, reviews = p.ReviewsCount, link = Link($"/pitches/{p.Slug}"),
    };

    private static object? Owner(Pitch p)
    {
        var partner = p.Venue.Partner;
        return partner is null ? null : new
        {
            id = partner.UserId, name = partner.User?.FullName, company = partner.BusinessName, email = partner.User?.Email,
            phone = partner.User?.PhoneNumber ?? p.Venue.Phone, telegram = (string?)null, telegramChatId = (string?)null,
        };
    }

    private Dictionary<string, object?> PitchAndPartner(Pitch p) => new() { ["pitch"] = PitchInfo(p), ["partner"] = Owner(p) };
}
