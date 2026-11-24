using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Inbox;

/// <summary>One notification of the bell. Category in camelCase (bookings, matches, news…); Tone info / success / warning / danger.</summary>
public sealed record InboxItemDto(long Id, string Category, string Title, string? Body, string? Link, string Tone, DateTime CreatedAt, bool Read);

/// <summary>The newest notifications (up to 50) and how many are unread.</summary>
public sealed record InboxDto(IReadOnlyList<InboxItemDto> Items, int Unread);

/// <summary>POST /me/notifications/read: these ids, or every one when Ids is empty.</summary>
public sealed record MarkReadRequest(IReadOnlyList<long>? Ids);

/// <summary>Whether this kind of notification also comes to the person's phone.</summary>
public sealed record PushSettingDto(string Category, bool Push);

/// <summary>PUT /me/notification-settings: the kinds to change.</summary>
public sealed record SetPushSettingsRequest(IReadOnlyList<PushSettingDto> Settings);

public interface IInboxRepository
{
    Task<InboxDto> LatestAsync(Guid userId, int take, CancellationToken ct);

    Task MarkReadAsync(Guid userId, IReadOnlyList<long>? ids, DateTime now, CancellationToken ct);

    /// <summary>The kinds the person switched off on the phone.</summary>
    Task<IReadOnlyList<NotificationCategory>> PushOffAsync(Guid userId, CancellationToken ct);

    Task SetPushAsync(Guid userId, IReadOnlyDictionary<NotificationCategory, bool> push, CancellationToken ct);
}

public interface IInboxService
{
    Task<InboxDto> LatestAsync(CancellationToken ct);

    Task MarkReadAsync(MarkReadRequest request, CancellationToken ct);

    Task<IReadOnlyList<PushSettingDto>> SettingsAsync(CancellationToken ct);

    Task<IReadOnlyList<PushSettingDto>> SetSettingsAsync(SetPushSettingsRequest request, CancellationToken ct);
}

/// <summary>The signed-in person's bell (server notifications) and which kinds reach their phone (all, unless switched off).</summary>
public sealed class InboxService(
    IInboxRepository inbox,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<MarkReadRequest> markRules,
    IValidator<SetPushSettingsRequest> settingsRules) : IInboxService
{
    /// <summary>The kinds a person can switch off on the phone (System stays on: security and account notices).</summary>
    public static readonly NotificationCategory[] Switchable =
    [
        NotificationCategory.Bookings, NotificationCategory.Matches, NotificationCategory.News, NotificationCategory.Fantasy,
        NotificationCategory.Orders, NotificationCategory.Money, NotificationCategory.Community, NotificationCategory.Support,
        NotificationCategory.Store, NotificationCategory.Designs, NotificationCategory.Level,
    ];

    public Task<InboxDto> LatestAsync(CancellationToken ct) => inbox.LatestAsync(Me(), 50, ct);

    public async Task MarkReadAsync(MarkReadRequest request, CancellationToken ct)
    {
        await markRules.ValidateAndThrowAsync(request, ct);
        await inbox.MarkReadAsync(Me(), request.Ids, clock.UtcNow, ct);
    }

    public async Task<IReadOnlyList<PushSettingDto>> SettingsAsync(CancellationToken ct)
    {
        var off = await inbox.PushOffAsync(Me(), ct);
        return Switchable.Select(c => new PushSettingDto(Name(c), !off.Contains(c))).ToList();
    }

    public async Task<IReadOnlyList<PushSettingDto>> SetSettingsAsync(SetPushSettingsRequest request, CancellationToken ct)
    {
        await settingsRules.ValidateAndThrowAsync(request, ct);
        var changes = request.Settings.ToDictionary(s => Parse(s.Category)!.Value, s => s.Push);
        await inbox.SetPushAsync(Me(), changes, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await SettingsAsync(ct);
    }

    public static string Name(NotificationCategory c) => char.ToLowerInvariant(c.ToString()[0]) + c.ToString()[1..];

    public static NotificationCategory? Parse(string? name) =>
        Switchable.Cast<NotificationCategory?>().FirstOrDefault(c => string.Equals(c.ToString(), name, StringComparison.OrdinalIgnoreCase));

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class MarkReadRequestValidator : AbstractValidator<MarkReadRequest>
{
    public MarkReadRequestValidator() =>
        RuleFor(x => x.Ids).Must(ids => ids is null || (ids.Count <= 200 && ids.All(i => i > 0))).WithMessage("Up to 200 notifications at a time.");
}

public sealed class SetPushSettingsRequestValidator : AbstractValidator<SetPushSettingsRequest>
{
    public SetPushSettingsRequestValidator()
    {
        RuleFor(x => x.Settings).NotEmpty().Must(s => s is null || s.Count <= 20);
        RuleForEach(x => x.Settings).Must(s => InboxService.Parse(s.Category) is not null).WithMessage("Unknown kind of notification.");
        RuleFor(x => x.Settings).Must(s => s is null || s.Select(x => x.Category.ToLowerInvariant()).Distinct().Count() == s.Count)
            .WithMessage("Each kind once.");
    }
}
