using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>
/// Factory sign-in (API §4.6): the owner registers a device once; on that device staff pick their name and type a 4-digit
/// PIN (5 wrong tries lock the PIN for 15 minutes). The session lasts 12 hours on that device.
/// </summary>
public interface IFactoryAuthService
{
    Task<DeviceTokenDto> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct);

    Task<IReadOnlyList<StaffNameDto>> StaffListAsync(string deviceToken, CancellationToken ct);

    Task<FactorySessionDto> PinAsync(string deviceToken, PinRequest request, CancellationToken ct);

    Task<FactoryMeDto> MeAsync(CancellationToken ct);
}

public sealed class FactoryAuthService(
    IFactoryAccess access,
    IFactoryRepository factories,
    IFactoryTokens tokens,
    IPinHasher pins,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IClock clock,
    IValidator<PinRequest> pinRules) : IFactoryAuthService
{
    public const int MaxWrongPins = 5;

    public async Task<DeviceTokenDto> RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 60)
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("name", "Name the device (up to 60 characters).")]);
        }
        var owner = await access.OwnerAsync(ct);
        var (token, hash) = tokens.NewDeviceToken();
        var device = new FactoryDevice { FactoryId = owner.FactoryId, Name = request.Name.Trim(), TokenHash = hash, LastSeenAt = clock.UtcNow };
        factories.AddDevice(device);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Factory, "device.registered", $"{owner.FactoryName} registered the device “{device.Name}”", "FactoryDevice", device.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return new DeviceTokenDto(device.Id, device.Name, token);
    }

    public async Task<IReadOnlyList<StaffNameDto>> StaffListAsync(string deviceToken, CancellationToken ct)
    {
        var device = await factories.DeviceAsync(deviceToken, ct) ?? throw new UnauthorizedException("This device is not registered.", "factory_device");
        return await factories.StaffNamesAsync(device.FactoryId, ct);
    }

    public async Task<FactorySessionDto> PinAsync(string deviceToken, PinRequest request, CancellationToken ct)
    {
        await pinRules.ValidateAndThrowAsync(request, ct);
        var device = await factories.DeviceAsync(deviceToken, ct) ?? throw new UnauthorizedException("This device is not registered.", "factory_device");
        var staff = await factories.StaffAsync(device.FactoryId, request.StaffId, ct);
        if (staff is null || !staff.IsActive)
        {
            throw new UnauthorizedException("Wrong name or PIN.", "bad_pin");
        }
        if (staff.LockedUntil > clock.UtcNow)
        {
            throw new LockedException("Too many wrong PINs. Try again in 15 minutes or ask the owner to reset it.", "pin_locked");
        }
        if (!pins.Verify(request.Pin, staff.PinHash))
        {
            staff.FailedPins++;
            staff.LockedUntil = staff.FailedPins >= MaxWrongPins ? clock.UtcNow.AddMinutes(15) : null;
            staff.FailedPins = staff.FailedPins >= MaxWrongPins ? (byte)0 : staff.FailedPins;
            await unitOfWork.SaveChangesAsync(ct);
            throw new UnauthorizedException("Wrong name or PIN.", "bad_pin");
        }
        (staff.FailedPins, staff.LockedUntil) = (0, null);
        await unitOfWork.SaveChangesAsync(ct);
        var expires = clock.UtcNow.AddHours(12);
        var scope = await factories.StaffScopeAsync(staff.Id, device.FactoryId, ct) ?? throw new UnauthorizedException();
        return new FactorySessionDto(tokens.CreateSession(staff.Id, device.Id, expires), expires, Me(scope));
    }

    public async Task<FactoryMeDto> MeAsync(CancellationToken ct) => Me(await access.CurrentAsync(ct));

    private static FactoryMeDto Me(FactoryScope s) =>
        new(s.FactoryId, s.FactoryName, s.StaffId, s.Name, s.Role.ToString(), FactoryRoles.Of(s.Role), s.IntakePaused, s.Status.ToString());
}

/// <summary>Hashes and checks 4-digit PINs (implemented in Infrastructure).</summary>
public interface IPinHasher
{
    byte[] Hash(string pin);

    bool Verify(string pin, byte[] hash);
}

public sealed class PinRequestValidator : AbstractValidator<PinRequest>
{
    public PinRequestValidator()
    {
        RuleFor(x => x.StaffId).GreaterThan(0);
        RuleFor(x => x.Pin).NotEmpty().Matches("^[0-9]{4}$").WithMessage("The PIN has 4 digits.");
    }
}
