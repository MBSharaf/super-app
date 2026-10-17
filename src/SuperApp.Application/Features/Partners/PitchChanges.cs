using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Application.Features.Partners;

/// <summary>Updates a pitch's child rows in place (no delete-and-add of the same keys).</summary>
public static class PitchChanges
{
    public static void SyncFacilities(Pitch pitch, IReadOnlySet<int> wanted)
    {
        foreach (var gone in pitch.Facilities.Where(f => !wanted.Contains(f.FacilityId)).ToList())
        {
            pitch.Facilities.Remove(gone);
        }
        var have = pitch.Facilities.Select(f => f.FacilityId).ToHashSet();
        foreach (var id in wanted.Where(id => !have.Contains(id)))
        {
            pitch.Facilities.Add(new PitchFacility { FacilityId = id });
        }
    }

    /// <summary>One row per weekday. A closed day keeps valid hours (the database requires close &gt; open).</summary>
    public static void SyncHours(Pitch pitch, IReadOnlyList<OpeningHourDto> days)
    {
        foreach (var d in days)
        {
            var row = pitch.OpeningHours.FirstOrDefault(h => h.DayOfWeek == d.DayOfWeek);
            if (row is null)
            {
                row = new OpeningHour { DayOfWeek = (byte)d.DayOfWeek };
                pitch.OpeningHours.Add(row);
            }
            row.IsClosed = d.IsClosed;
            row.OpenHour = (byte)(d.IsClosed && d.CloseHour <= d.OpenHour ? 0 : d.OpenHour);
            row.CloseHour = (byte)(d.IsClosed && d.CloseHour <= d.OpenHour ? 1 : d.CloseHour);
        }
    }
}
