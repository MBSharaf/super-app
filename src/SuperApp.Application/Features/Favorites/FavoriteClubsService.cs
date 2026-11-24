using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Application.Features.Favorites;

/// <summary>A club someone can follow: names in both languages and its badge.</summary>
public sealed record FollowClubDto(long Id, string Name, string NameAr, string ShortName, string? Logo);

/// <summary>The clubs of one competition's current season, for the picker.</summary>
public sealed record FollowCompetitionDto(string Key, string Name, string NameAr, IReadOnlyList<FollowClubDto> Clubs);

/// <summary>PUT /me/favorite-clubs: up to three club ids, favourite first. An empty list clears them.</summary>
public sealed record SetFavoriteClubsRequest(IReadOnlyList<long> ClubIds);

public interface IFavoriteClubsRepository
{
    /// <summary>Active competitions with the clubs of their current season, in the site's order.</summary>
    Task<IReadOnlyList<FollowCompetitionDto>> PickableAsync(CancellationToken ct);

    /// <summary>Which of these ids are clubs playing a current season of an active competition.</summary>
    Task<IReadOnlyList<long>> PickableIdsAsync(IReadOnlyList<long> ids, CancellationToken ct);

    Task<IReadOnlyList<FollowClubDto>> MineAsync(Guid userId, CancellationToken ct);

    /// <summary>Replaces the person's clubs with these, in this order (rank 1 first).</summary>
    Task ReplaceAsync(Guid userId, IReadOnlyList<long> clubIds, DateTime now, CancellationToken ct);
}

public interface IFavoriteClubsService
{
    Task<IReadOnlyList<FollowCompetitionDto>> PickableAsync(CancellationToken ct);

    Task<IReadOnlyList<FollowClubDto>> MineAsync(CancellationToken ct);

    Task<IReadOnlyList<FollowClubDto>> SetAsync(SetFavoriteClubsRequest request, CancellationToken ct);
}

/// <summary>The clubs a person follows (up to three): their line-ups, kick-off, goals and final whistle reach that person.</summary>
public sealed class FavoriteClubsService(
    IFavoriteClubsRepository clubs,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<SetFavoriteClubsRequest> rules) : IFavoriteClubsService
{
    public const int Max = 3;

    public Task<IReadOnlyList<FollowCompetitionDto>> PickableAsync(CancellationToken ct) => clubs.PickableAsync(ct);

    public Task<IReadOnlyList<FollowClubDto>> MineAsync(CancellationToken ct) => clubs.MineAsync(Me(), ct);

    public async Task<IReadOnlyList<FollowClubDto>> SetAsync(SetFavoriteClubsRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var ids = request.ClubIds.Distinct().ToList();
        var known = await clubs.PickableIdsAsync(ids, ct);
        if (known.Count != ids.Count)
        {
            throw new BusinessRuleException("Pick clubs from the list.", "unknown_club");
        }
        await clubs.ReplaceAsync(me, ids, clock.UtcNow, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await clubs.MineAsync(me, ct);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class SetFavoriteClubsRequestValidator : AbstractValidator<SetFavoriteClubsRequest>
{
    public SetFavoriteClubsRequestValidator()
    {
        RuleFor(x => x.ClubIds).NotNull()
            .Must(ids => ids is null || ids.Distinct().Count() <= FavoriteClubsService.Max).WithMessage("Pick up to 3 clubs.")
            .Must(ids => ids is null || ids.All(id => id > 0)).WithMessage("Pick clubs from the list.");
    }
}
