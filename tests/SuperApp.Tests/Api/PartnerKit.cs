using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Test helpers for the partner dashboard: people, a partner business, a venue and a pitch.</summary>
public sealed class PartnerKit(AccountsApiFactory api)
{
    public static string Day(int daysAhead) => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead)).ToString("yyyy-MM-dd");

    /// <summary>A signed-in person; with <paramref name="partner"/> they own a partner business (as after an approved application).</summary>
    public async Task<(ApiClient Client, Guid UserId)> PersonAsync(bool partner)
    {
        var client = await api.ClientAsync();
        var email = $"p.{Guid.NewGuid():N}"[..20] + "@test.example";
        var (status, body) = await client.PostAsync("/api/v1/auth/sign-up", new { fullName = "Test Person", email, phone = "+2010" + Random.Shared.Next(10_000_000, 99_999_999), password = "Football2026", acceptTerms = true });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        await client.RefreshCsrfAsync();
        var userId = await api.DbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        if (partner)
        {
            await api.DbAsync(db =>
            {
                db.Set<Partner>().Add(new Partner { UserId = userId, BusinessName = "Test Arena Co", PayoutDetail = "test" });
                return db.SaveChangesAsync();
            });
        }
        return (client, userId);
    }

    public Task<int> AreaAsync() => api.DbAsync(db => db.Set<Area>().OrderBy(a => a.Id).Select(a => a.Id).FirstAsync());

    public static object[] Week(int open = 9, int close = 26) =>
        Enumerable.Range(0, 7).Select(d => (object)new { dayOfWeek = d, openHour = open, closeHour = close, isClosed = false }).ToArray();

    public static object Pitch(long venueId, string name = "Test Five", decimal price = 600, string? rowVersion = null) => new
    {
        venueId, name, format = 5, surface = "ArtificialTurf", isIndoor = false, pricePerHour = price, peakPricePerHour = 750m, peakFromHour = 18,
        descriptionEn = "Floodlit five-a-side.", descriptionAr = (string?)null, policyKey = "standard", allowCash = true,
        facilities = new[] { "lights", "parking" }, openingHours = Week(), rowVersion,
    };

    /// <summary>A partner with one venue and one pitch (waiting for review, or live).</summary>
    public async Task<(ApiClient Client, Guid UserId, long VenueId, JsonElement Pitch)> PartnerWithPitchAsync(bool live = false)
    {
        var (client, userId) = await PersonAsync(partner: true);
        var venue = await client.PostAsync("/api/v1/partner/venues", new { name = "Test Arena", areaId = await AreaAsync(), address = "1 Test Street, Cairo", lat = 30.05m, lng = 31.23m, phone = (string?)null });
        venue.Status.ShouldBe(HttpStatusCode.Created, venue.Body.ToString());
        var venueId = venue.Body.Data().GetProperty("id").GetInt64();
        var pitch = await client.PostAsync("/api/v1/partner/pitches", Pitch(venueId));
        pitch.Status.ShouldBe(HttpStatusCode.Created, pitch.Body.ToString());
        if (live)
        {
            var id = pitch.Body.Data().GetProperty("id").GetInt64();
            await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Pitches.Pitch>().Where(p => p.Id == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PitchStatus.Live)));
        }
        return (client, userId, venueId, pitch.Body.Data());
    }

    /// <summary>Adds someone to the partner's staff with a role.</summary>
    public Task AddStaffAsync(long venueId, Guid userId, PartnerStaffRole role) => api.DbAsync(async db =>
    {
        var partnerId = await db.Set<Venue>().Where(v => v.Id == venueId).Select(v => v.PartnerId).SingleAsync();
        db.Set<PartnerStaffMember>().Add(new PartnerStaffMember { PartnerId = partnerId, UserId = userId, Role = role });
        return await db.SaveChangesAsync();
    });
}
