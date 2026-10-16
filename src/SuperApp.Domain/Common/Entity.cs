namespace SuperApp.Domain.Common;

/// <summary>Root of every entity: an identity of type <typeparamref name="TKey"/>.</summary>
public abstract class Entity<TKey>
    where TKey : notnull
{
    public TKey Id { get; set; } = default!;
}
