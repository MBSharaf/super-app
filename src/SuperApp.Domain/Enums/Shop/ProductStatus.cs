namespace SuperApp.Domain.Enums;

public enum ProductStatus : byte
{
    Draft = 0,
    Published = 1,
    Unpublished = 2,

    /// <summary>The store asked to sell it; Super App checks it first (sellers' and partners' products).</summary>
    WaitingForReview = 3,

    /// <summary>Not approved — the store sees why (<c>ReviewNote</c>), fixes it and asks again.</summary>
    Rejected = 4,
}
