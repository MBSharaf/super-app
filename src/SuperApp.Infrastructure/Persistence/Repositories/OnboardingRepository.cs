using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Creates the partner, seller or factory records when an application is approved (saved by the caller's unit of work).</summary>
public sealed partial class OnboardingRepository(AppDbContext db) : IOnboardingRepository
{
    public async Task OnboardPitchOwnerAsync(Guid userId, PitchOwnerAnswers answers, IReadOnlyList<long> photoFileIds, CancellationToken ct)
    {
        var partner = new Partner { UserId = userId, BusinessName = answers.PitchName.Trim(), PayoutMethod = Payout(answers.PayoutMethod), PayoutDetail = answers.PayoutDetail.Trim() };
        var venue = new Venue { Partner = partner, Name = answers.PitchName.Trim(), AreaId = answers.AreaId, Address = answers.Address.Trim(), Lat = answers.Lat, Lng = answers.Lng, Phone = answers.Phone };
        var policyId = await db.Set<CancellationPolicy>().Where(p => p.Key == "standard").Select(p => p.Id).SingleAsync(ct);
        for (var i = 0; i < answers.Count; i++)
        {
            var name = answers.Count == 1 ? answers.PitchName.Trim() : $"{answers.PitchName.Trim()} — Pitch {i + 1}";
            var pitch = new Pitch
            {
                Venue = venue, Name = name, Slug = await FreeSlugAsync(name, ct), Format = (byte)answers.Formats[i % answers.Formats.Count],
                Surface = PitchSurface.ArtificialTurf, PricePerHour = answers.Price, CancellationPolicyId = policyId, Status = PitchStatus.Live,
            };
            pitch.Photos = photoFileIds.Select((fileId, n) => new PitchPhoto { FileId = fileId, SortOrder = n + 1 }).ToList();
            db.Set<Pitch>().Add(pitch);
        }
    }

    public async Task OnboardSellerAsync(Guid userId, SellerAnswers answers, CancellationToken ct)
    {
        var handle = string.IsNullOrEmpty(answers.Handle) ? await FreeHandleAsync(answers.StoreName, ct) : answers.Handle;
        var store = new Store
        {
            OwnerUserId = userId, Type = StoreType.Seller, Handle = handle, Name = answers.StoreName.Trim(), About = answers.Bio?.Trim(),
            AccentColor = "#0B6E4F", Status = StoreStatus.Active,
        };
        var firstLevel = await db.Set<SellerLevel>().OrderBy(l => l.Rank).Select(l => l.Id).FirstAsync(ct);
        db.Set<Seller>().Add(new Seller
        {
            UserId = userId, Store = store, LevelId = firstLevel, Status = SellerStatus.Active,
            Mode = answers.Mode switch { "store" => SellerMode.OwnProducts, "both" => SellerMode.Both, _ => SellerMode.PrintOnDemand },
            PayoutMethod = Payout(answers.PayoutMethod), PayoutDetail = answers.PayoutDetail.Trim(),
        });
    }

    public Task OnboardFactoryAsync(Guid userId, FactoryAnswers answers, CancellationToken ct)
    {
        db.Set<Factory>().Add(new Factory
        {
            OwnerUserId = userId, Name = answers.FactoryName.Trim(), Address = answers.Address.Trim(), AreaId = answers.AreaId, CapacityPerDay = answers.CapacityPerDay,
            SlaHours = 48, Techniques = string.Join(',', answers.Techniques), Status = FactoryStatus.Active, PayoutDetail = answers.PayoutDetail.Trim(),
        });
        return Task.CompletedTask;
    }

    private static PayoutMethod Payout(string method) => method == "InstaPay" ? PayoutMethod.InstaPay : PayoutMethod.Bank;

    private async Task<string> FreeSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slug(name, "pitch");
        var taken = await db.Set<Pitch>().IgnoreQueryFilters().Where(p => p.Slug.StartsWith(slug)).Select(p => p.Slug).ToListAsync(ct);
        taken.AddRange(db.ChangeTracker.Entries<Pitch>().Select(e => e.Entity.Slug));
        return Free(slug, taken);
    }

    private async Task<string> FreeHandleAsync(string name, CancellationToken ct)
    {
        var handle = Slug(name, "store");
        var taken = await db.Set<Store>().IgnoreQueryFilters().Where(s => s.Handle.StartsWith(handle)).Select(s => s.Handle).ToListAsync(ct);
        return Free(handle, taken);
    }

    internal static string Free(string baseName, List<string> taken)
    {
        var candidate = baseName;
        for (var i = 2; taken.Contains(candidate); i++)
        {
            candidate = $"{baseName}-{i}";
        }
        return candidate;
    }

    /// <summary>"Turf 22 Arena" → "turf-22-arena"; names in Arabic only fall back to the given word.</summary>
    internal static string Slug(string text, string fallback)
    {
        var slug = NotSlug().Replace(text.ToLowerInvariant(), "-").Trim('-');
        slug = slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
        return slug.Length >= 3 ? slug : fallback;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlug();
}
