namespace SuperApp.Domain.Enums;

/// <summary>The booking lengths a padel court's owner offers (owner, 2026-11-21: 60, 90 or 120 minutes).</summary>
[Flags]
public enum CourtDurations : byte
{
    None = 0,
    Min60 = 1,
    Min90 = 2,
    Min120 = 4,
}

public static class CourtDurationsExtensions
{
    public static readonly int[] All = [60, 90, 120];

    public static CourtDurations FromMinutes(IEnumerable<int> minutes) =>
        minutes.Aggregate(CourtDurations.None, (f, m) => f | m switch { 60 => CourtDurations.Min60, 90 => CourtDurations.Min90, 120 => CourtDurations.Min120, _ => CourtDurations.None });

    public static IReadOnlyList<int> ToMinutes(this CourtDurations d) =>
        All.Where(m => d.HasFlag(FromMinutes([m]))).ToList();

    public static bool Allows(this CourtDurations d, int minutes) => d.ToMinutes().Contains(minutes);
}
