using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// The partner business the caller works for. <c>Role</c> is null for the owner, otherwise the staff role
/// (Manager, CheckIn, Accountant).
/// </summary>
public sealed record PartnerScope(long PartnerId, Guid OwnerUserId, string BusinessName, PartnerStatus Status, PartnerStaffRole? Role)
{
    public bool IsOwner => Role is null;

    /// <summary>The owner and managers run the pitches; check-in staff and accountants only see.</summary>
    public bool CanManage => Role is null or PartnerStaffRole.Manager;
}

/// <summary>A partner's venues, pitches and closures in the database. Ids from other partners are never returned.</summary>
public interface IPartnerRepository
{
    /// <summary>The partner the person owns, or the one they are active staff of.</summary>
    Task<PartnerScope?> ScopeAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<VenueDto>> VenuesAsync(long partnerId, CancellationToken ct);

    /// <summary>A venue of this partner, tracked for changes.</summary>
    Task<Venue?> VenueAsync(long partnerId, long venueId, CancellationToken ct);

    void AddVenue(Venue venue);

    Task<bool> AreaExistsAsync(int areaId, CancellationToken ct);

    Task<IReadOnlyList<MyPitchDto>> PitchesAsync(long partnerId, CancellationToken ct);

    Task<PitchEditDto?> PitchForEditAsync(long partnerId, long pitchId, CancellationToken ct);

    /// <summary>A pitch of this partner with its hours and facilities, tracked for changes.</summary>
    Task<Pitch?> PitchAsync(long partnerId, long pitchId, CancellationToken ct);

    /// <summary>The edit was based on this version: a newer saved change makes the save fail with 409.</summary>
    void ExpectVersion(Pitch pitch, byte[] rowVersion);

    void AddPitch(Pitch pitch);

    Task<string> FreeSlugAsync(string name, CancellationToken ct);

    Task<int?> PolicyIdAsync(string key, CancellationToken ct);

    /// <summary>Facility ids by key; unknown keys are missing from the result.</summary>
    Task<IReadOnlyDictionary<string, int>> FacilityIdsAsync(IReadOnlyList<string> keys, CancellationToken ct);

    Task<IReadOnlyList<ClosureDto>> ClosuresAsync(long pitchId, DateOnly from, CancellationToken ct);

    Task<Closure?> ClosureAsync(long pitchId, long closureId, CancellationToken ct);

    void AddClosure(Closure closure);

    void RemoveClosure(Closure closure);

    /// <summary>Active bookings with an hour inside the range (calendar dates and hours).</summary>
    Task<int> BookingsInRangeAsync(long pitchId, DateOnly from, DateOnly to, int? fromHour, int? toHour, CancellationToken ct);
}
