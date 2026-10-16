namespace SuperApp.Domain.Common;

/// <summary>Has <c>CreatedAt</c> / <c>UpdatedAt</c> (UTC), filled automatically on save.</summary>
public interface ITimestamped
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>Soft delete — the ERD tag [S]. Deleted rows are hidden by a global query filter.</summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>Optimistic concurrency — the ERD tag [V]. A stale update fails with a conflict (HTTP 409).</summary>
public interface IHasRowVersion
{
    byte[] RowVersion { get; set; }
}

/// <summary>Records who created the row — the ERD tag [A] (FK to auth.Users, NO ACTION).</summary>
public interface IHasAuthor
{
    Guid? CreatedById { get; set; }
}
