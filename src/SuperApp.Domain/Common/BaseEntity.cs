namespace SuperApp.Domain.Common;

/// <summary>
/// Business table with a <c>bigint</c> identity and timestamps — the ERD tag [B].
/// <c>CreatedAt</c> defaults to <c>SYSUTCDATETIME()</c>; both are set by the save interceptor.
/// </summary>
public abstract class BaseEntity : Entity<long>, ITimestamped
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Business table with an <c>int</c> identity and timestamps (small registries such as integrations).</summary>
public abstract class IntBaseEntity : Entity<int>, ITimestamped
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Lookup table with an <c>int</c> identity — the ERD tag [L].</summary>
public abstract class LookupEntity : Entity<int>
{
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
