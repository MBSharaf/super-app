using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Application.Features.Push;

/// <summary>POST /me/push-devices: the app's Firebase token. Platform is "android" (or "ios" later).</summary>
public sealed record RegisterPushDeviceRequest(string Token, string Platform);

/// <summary>POST /me/push-devices/remove: stop push to this phone (sign-out).</summary>
public sealed record RemovePushDeviceRequest(string Token);

public interface IPushDeviceRepository
{
    /// <summary>The device row for this token, whoever it belongs to, tracked for changes.</summary>
    Task<PushDevice?> ByTokenAsync(string token, CancellationToken ct);

    void Add(PushDevice device);

    void Remove(PushDevice device);
}

/// <summary>Phones of the signed-in person that get push notifications.</summary>
public interface IPushDevicesService
{
    Task RegisterAsync(RegisterPushDeviceRequest request, CancellationToken ct);

    Task RemoveAsync(RemovePushDeviceRequest request, CancellationToken ct);
}

public sealed class PushDevicesService(
    IPushDeviceRepository devices,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<RegisterPushDeviceRequest> registerRules,
    IValidator<RemovePushDeviceRequest> removeRules) : IPushDevicesService
{
    /// <summary>A token seen again moves to whoever is signed in now (a shared phone), so one person never gets another's alerts.</summary>
    public async Task RegisterAsync(RegisterPushDeviceRequest request, CancellationToken ct)
    {
        await registerRules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var device = await devices.ByTokenAsync(request.Token, ct);
        if (device is null)
        {
            devices.Add(new PushDevice { UserId = me, Token = request.Token, Platform = request.Platform, CreatedAt = clock.UtcNow, LastSeenAt = clock.UtcNow });
        }
        else
        {
            device.UserId = me;
            device.Platform = request.Platform;
            device.LastSeenAt = clock.UtcNow;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(RemovePushDeviceRequest request, CancellationToken ct)
    {
        await removeRules.ValidateAndThrowAsync(request, ct);
        var me = Me();
        var device = await devices.ByTokenAsync(request.Token, ct);
        if (device is null || device.UserId != me)
        {
            return;
        }
        devices.Remove(device);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class RegisterPushDeviceRequestValidator : AbstractValidator<RegisterPushDeviceRequest>
{
    public RegisterPushDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512).Matches("^[A-Za-z0-9_:\\-\\.]+$").WithMessage("This is not a valid device token.");
        RuleFor(x => x.Platform).NotEmpty().Must(p => p is "android" or "ios").WithMessage("Platform must be android or ios.");
    }
}

public sealed class RemovePushDeviceRequestValidator : AbstractValidator<RemovePushDeviceRequest>
{
    public RemovePushDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
    }
}
