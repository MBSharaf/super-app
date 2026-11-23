using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Automation;

/// <summary>The details of each event, field for field like the demo site's (js/services/automation.js).</summary>
public sealed partial class SitePayloads
{
    private const decimal StoreCommission = 0.05m;

    private object BookingOf(Booking b) => new
    {
        code = b.Code, status = Lower(b.Status), date = Iso(b.Date), startTime = Hh(b.StartHour, b.StartMinute), duration = b.LengthMinutes / 60m,
        endTime = b.KickOff().AddMinutes(b.LengthMinutes).ToString("HH:mm"), players = b.Pitch.Format * 2, sport = Lower(b.Pitch.Sport),
        listPrice = b.PitchAmount + b.DiscountAmount, discountAmount = b.DiscountAmount, promoCode = b.PromoCode?.Code, extras = b.ExtrasAmount,
        pitchTotal = b.PitchAmount, serviceFee = b.ServiceFee, total = b.Total, currency = "EGP", payment = b.PayAtVenue ? "cash" : "online",
        paymentMode = Lower(b.PaymentMode), createdAt = b.CreatedAt, depositPaid = b.Deposit, payAtPitch = b.BalanceDue, cancelPolicy = b.PolicyKey,
        packageId = b.Package?.Code, refundAmount = b.RefundAmount, cancelledAt = b.CancelledAt, link = Link($"/booking/{b.Code}"),
    };

    private IQueryable<Booking> Bookings() => db.Set<Booking>().IgnoreQueryFilters()
        .Include(b => b.Customer).Include(b => b.PromoCode).Include(b => b.Package)
        .Include(b => b.Pitch).ThenInclude(p => p.Venue).ThenInclude(v => v.Area).ThenInclude(a => a.Governorate)
        .Include(b => b.Pitch).ThenInclude(p => p.Venue).ThenInclude(v => v.Partner).ThenInclude(pa => pa.User)
        .Include(b => b.Pitch).ThenInclude(p => p.CancellationPolicy);

    private async Task<Dictionary<string, object?>?> BookingAsync(string code, CancellationToken ct)
    {
        var b = await Bookings().FirstOrDefaultAsync(x => x.Code == code, ct);
        if (b is null)
        {
            return null;
        }
        var d = PitchAndPartner(b.Pitch);
        d["booking"] = BookingOf(b);
        d["customer"] = b.Customer is null ? new { id = (Guid?)null, name = b.WalkInName, email = (string?)null, phone = b.WalkInPhone, city = (string?)null } : Person(b.Customer);
        return d;
    }

    private async Task<Dictionary<string, object?>?> PackageAsync(string code, CancellationToken ct)
    {
        var pack = await db.Set<Package>().IgnoreQueryFilters().Include(p => p.Customer).FirstOrDefaultAsync(p => p.Code == code, ct);
        var pitch = pack is null ? null : await Pitches(true).FirstOrDefaultAsync(p => p.Id == pack.PitchId, ct);
        if (pack is null || pitch is null)
        {
            return null;
        }
        var sessions = await Bookings().Where(b => b.PackageId == pack.Id).OrderBy(b => b.Date).ToListAsync(ct);
        var d = PitchAndPartner(pitch);
        d["package"] = new
        {
            id = pack.Code, kind = "weekly", weeks = pack.MatchCount, sessionsBooked = sessions.Count, weekday = ((DayOfWeek)pack.DayOfWeek).ToString(),
            startTime = Hh(pack.StartHour), duration = pack.Duration, startDate = Iso(pack.StartsOn), endDate = Iso(pack.EndsOn), discountPercent = pack.DiscountPercent,
            total = pack.TotalPrice, currency = "EGP", payment = "online", status = Lower(pack.Status), createdAt = pack.CreatedAt,
        };
        d["sessions"] = sessions.Select(BookingOf).ToList();
        d["customer"] = Person(pack.Customer);
        return d;
    }

    private async Task<Dictionary<string, object?>?> OrderAsync(string code, CancellationToken ct)
    {
        var o = await db.Set<Order>().IgnoreQueryFilters().Include(x => x.Customer).Include(x => x.PromoCode)
            .Include(x => x.Items).ThenInclude(i => i.Store).ThenInclude(s => s.OwnerUser).FirstOrDefaultAsync(x => x.Code == code, ct);
        if (o is null)
        {
            return null;
        }
        var stores = o.Items.GroupBy(i => i.Store).Select(StoreOf).ToList();
        return new Dictionary<string, object?>
        {
            ["order"] = new
            {
                code = o.Code, status = Lower(o.Status), placedAt = o.CreatedAt, deliverySpeed = Lower(o.DeliverySpeed), itemsCount = o.Items.Sum(i => i.Qty),
                subtotal = o.Subtotal, discount = o.DiscountAmount, promoCode = o.PromoCode?.Code, deliveryFee = o.DeliveryFee, total = o.Total, currency = "EGP",
                payment = o.PaymentMethod == OrderPaymentMethod.CashOnDelivery ? "cod" : "card", link = Link($"/order/{o.Code}"),
            },
            ["customer"] = Person(o.Customer),
            ["address"] = string.IsNullOrWhiteSpace(o.AddressJson) ? null : JsonSerializer.Deserialize<JsonElement>(o.AddressJson),
            ["stores"] = stores.Select(s => s.Store).ToList(),
            ["items"] = stores.SelectMany(s => s.Items).ToList(),
        };
    }

    private (object Store, IEnumerable<object> Items) StoreOf(IGrouping<Store, OrderItem> g)
    {
        var st = g.Key;
        var official = st.Type == StoreType.Official;
        var items = g.Select(i => (object)new
        {
            productId = i.ProductVariantId, name = i.Title, size = i.Size, color = i.Color, qty = i.Qty, unitPrice = i.UnitPrice, lineTotal = i.UnitPrice * i.Qty,
            store = st.Name, storeId = st.Handle,
        }).ToList();
        var itemsTotal = g.Sum(i => i.UnitPrice * i.Qty);
        var commission = official ? 0 : g.Sum(i => i.CommissionAmount);
        var store = new
        {
            storeId = st.Handle, name = st.Name, handle = st.Handle, official, tagline = st.Tagline,
            owner = st.OwnerUser is null ? null : new { id = st.OwnerUser.Id, name = st.OwnerUser.FullName, email = st.OwnerUser.Email, phone = st.OwnerUser.PhoneNumber },
            commissionRate = official ? 0 : StoreCommission, items, itemsCount = g.Sum(i => i.Qty), itemsTotal, commission, storeEarning = itemsTotal - commission,
        };
        return (store, items);
    }

    private async Task<Dictionary<string, object?>?> ReviewAsync(string bookingCode, CancellationToken ct)
    {
        var r = await db.Set<Review>().IgnoreQueryFilters().Include(x => x.User).Include(x => x.Booking)
            .Where(x => x.Booking.Code == bookingCode).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var pitch = r is null ? null : await Pitches(true).FirstOrDefaultAsync(p => p.Id == r.PitchId, ct);
        if (r is null || pitch is null)
        {
            return null;
        }
        return new Dictionary<string, object?>
        {
            ["review"] = new
            {
                id = r.Id, stars = r.Stars, sentiment = r.Stars >= 4 ? "good" : r.Stars <= 2 ? "bad" : "neutral", text = r.Text, bookingCode,
                by = r.User?.FullName, at = r.CreatedAt, replyLink = Link($"/dash/reviews?id={r.Id}"),
            },
            ["pitch"] = PitchInfo(pitch), ["owner"] = Owner(pitch),
        };
    }

    private async Task<Dictionary<string, object?>?> SlotAsync(string pitchSlug, DateTime occurredAt, CancellationToken ct)
    {
        var pitch = await Pitches(true).FirstOrDefaultAsync(p => p.Slug == pitchSlug, ct);
        var slot = pitch is null ? null : await db.Set<FreeSlot>().Where(s => s.PitchId == pitch.Id && s.CreatedAt <= occurredAt.AddMinutes(1))
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        if (pitch is null || slot is null)
        {
            return null;
        }
        var alerts = await db.Set<SlotAlert>().Include(a => a.User)
            .Where(a => a.IsActive && (a.PitchId == pitch.Id || (a.PitchId == null && a.AreaId == pitch.Venue.AreaId))
                && (a.FromHour == null || a.FromHour <= slot.Hour) && (a.ToHour == null || a.ToHour > slot.Hour)).ToListAsync(ct);
        var price = pitch.PeakPricePerHour is { } peak && pitch.PeakFromHour is { } from && slot.Hour >= from ? peak : pitch.PricePerHour;
        return new Dictionary<string, object?>
        {
            ["slot"] = new
            {
                id = slot.Id, date = Iso(slot.Date), startTime = Hh(slot.Hour), endTime = Hh(slot.Hour + 1), duration = 1, discountPercent = slot.DiscountPercent, price,
                priceAfter = Math.Round(price * (1 - slot.DiscountPercent / 100m)), currency = "EGP",
                bookLink = Link($"/book/{pitch.Slug}?date={Iso(slot.Date)}&hour={slot.Hour}&dur=1"),
            },
            ["pitch"] = PitchInfo(pitch), ["owner"] = Owner(pitch),
            ["audience"] = new
            {
                channel = "notification", who = "players following this area or pitch", area = pitch.Venue.Area?.NameEn, city = pitch.Venue.Area?.Governorate?.NameEn,
                format = $"{pitch.Format}-a-side", recipients = alerts.Select(a => Person(a.User)).Distinct().ToList(),
                alerts = alerts.Select(a => new { id = a.Id, pitchId = a.PitchId, areaId = a.AreaId, from = a.FromHour is { } f ? Hh(f) : null, to = a.ToHour is { } t ? Hh(t) : null }).ToList(),
            },
        };
    }

    private async Task<Dictionary<string, object?>?> PitchEventAsync(string slug, bool removed, DateTime at, CancellationToken ct)
    {
        var pitch = await Pitches(true).FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (pitch is null)
        {
            return null;
        }
        return new Dictionary<string, object?> { ["pitch"] = PitchInfo(pitch, removed ? "removed" : null), ["owner"] = Owner(pitch), ["at"] = at };
    }

    private async Task<Dictionary<string, object?>?> CancellationAsync(string code, JsonElement? data, CancellationToken ct)
    {
        var r = await db.Set<CancellationRequest>().IgnoreQueryFilters().Include(x => x.Customer).Include(x => x.DecidedBy).FirstOrDefaultAsync(x => x.Code == code, ct);
        if (r is null)
        {
            return null;
        }
        var d = new Dictionary<string, object?>
        {
            ["cancellation"] = new
            {
                requestId = r.Code, status = Lower(r.Status), type = Lower(r.Kind), referenceId = r.Reference, item = r.Label,
                link = Prop(data ?? default, "link")?.GetString() is { } l ? Link(l.TrimStart('#')) : null, paidOnline = r.PaidOnline, refundIfApproved = r.RefundIfApproved,
                refunded = r.Refunded, currency = "EGP", cancelPolicy = Prop(data ?? default, "cancelPolicy")?.GetString(), reason = r.Reason, createdAt = r.CreatedAt,
                replyBy = r.DueAt, replyWithinHours = (int)Math.Round((r.DueAt - r.CreatedAt).TotalHours), channel = "chatbot",
                decision = r.DecidedAt is null ? null : new { status = Lower(r.Status), note = r.DecisionNote, by = r.DecidedBy?.FullName, at = r.DecidedAt },
                adminLink = Link($"/admin/cancellations?id={r.Code}"),
            },
            ["customer"] = Person(r.Customer),
        };
        if (r.Kind == CancellationKind.Booking && await Bookings().FirstOrDefaultAsync(b => b.Code == r.Reference, ct) is { } b)
        {
            d["pitch"] = PitchInfo(b.Pitch);
            d["partner"] = Owner(b.Pitch);
        }
        return d;
    }
}
