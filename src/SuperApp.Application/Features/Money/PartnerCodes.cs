using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

/// <summary>A venue's booking code. <c>DiscountType</c>: Percent or Fixed; <c>PitchIds</c> empty = all my pitches.</summary>
public sealed record PartnerCodeRequest(string Code, string DiscountType, decimal Value, decimal MinSpend, int? MaxUses, int? PerCustomer,
    DateOnly StartsOn, DateOnly? EndsOn, string? Label, IReadOnlyList<long>? PitchIds);

public sealed record PartnerCodeDto(long Id, string Code, string DiscountType, decimal Value, decimal MinSpend, int? MaxUses, int? PerCustomer,
    int UsesCount, DateOnly StartsOn, DateOnly? EndsOn, string Status, string? Label, IReadOnlyList<long> PitchIds);

public interface IPartnerCodeStore
{
    Task<IReadOnlyList<PromoCode>> MineAsync(Guid ownerUserId, CancellationToken ct);

    Task<PromoCode?> FindAsync(Guid ownerUserId, long id, CancellationToken ct);

    Task<bool> CodeTakenAsync(string code, long? exceptId, CancellationToken ct);

    Task<IReadOnlyList<long>> MyPitchIdsAsync(long partnerId, CancellationToken ct);

    void Add(PromoCode code);
}

/// <summary>/partner/codes — the owner's own codes for bookings (the venue pays the discount). Owners and managers.</summary>
public sealed class PartnerCodesService(IPartnerAccess access, IPartnerCodeStore store, IUnitOfWork unitOfWork, IAuditLogger audit, IClock clock, IValidator<PartnerCodeRequest> rules)
{
    public async Task<IReadOnlyList<PartnerCodeDto>> ListAsync(CancellationToken ct) =>
        (await store.MineAsync((await access.ManageAsync(ct)).OwnerUserId, ct)).Select(ToDto).ToList();

    public async Task<PartnerCodeDto> CreateAsync(PartnerCodeRequest request, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var code = new PromoCode { Funding = CodeFunding.OwnerFunded, OwnerUserId = scope.OwnerUserId, Scope = CodeScope.Bookings, Status = CodeStatus.Active };
        await ApplyAsync(code, request, scope, null, ct);
        store.Add(code);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "code.created", $"Code {code.Code} ({code.Value:0.##} {code.DiscountType})", "PromoCode", code.Id.ToString(), ct: ct);
        return ToDto(code);
    }

    public async Task<PartnerCodeDto> UpdateAsync(long id, PartnerCodeRequest request, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var code = await store.FindAsync(scope.OwnerUserId, id, ct) ?? throw new NotFoundException("Code not found.");
        await ApplyAsync(code, request, scope, id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(code);
    }

    /// <summary>Pause or resume.</summary>
    public async Task<PartnerCodeDto> PauseAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var code = await store.FindAsync(scope.OwnerUserId, id, ct) ?? throw new NotFoundException("Code not found.");
        code.Status = code.Status == CodeStatus.Active ? CodeStatus.Paused : CodeStatus.Active;
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(code);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var code = await store.FindAsync(scope.OwnerUserId, id, ct) ?? throw new NotFoundException("Code not found.");
        code.IsDeleted = true;
        code.DeletedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(PromoCode code, PartnerCodeRequest r, PartnerScope scope, long? id, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(r, ct);
        var text = r.Code.Trim().ToUpperInvariant();
        if (await store.CodeTakenAsync(text, id, ct))
        {
            throw new ConflictException("This code is already in use. Pick another one.", "code_exists");
        }
        var mine = await store.MyPitchIdsAsync(scope.PartnerId, ct);
        var pitches = r.PitchIds ?? [];
        if (pitches.Any(p => !mine.Contains(p)))
        {
            throw new BusinessRuleException("Pick your own pitches.", "not_your_pitch");
        }
        code.Code = text;
        code.DiscountType = Enum.Parse<DiscountType>(r.DiscountType, true);
        code.Value = r.Value;
        code.MinSpend = r.MinSpend;
        code.MaxUses = r.MaxUses;
        code.PerCustomer = r.PerCustomer;
        code.StartsOn = r.StartsOn;
        code.EndsOn = r.EndsOn;
        code.Label = r.Label?.Trim();
        code.Pitches.Clear();
        foreach (var p in pitches.Distinct())
        {
            code.Pitches.Add(new PromoCodePitch { PitchId = p, CreatedAt = clock.UtcNow });
        }
    }

    private static PartnerCodeDto ToDto(PromoCode c) => new(c.Id, c.Code, c.DiscountType.ToString(), c.Value, c.MinSpend, c.MaxUses, c.PerCustomer,
        c.UsesCount, c.StartsOn, c.EndsOn, c.Status.ToString(), c.Label, c.Pitches.Select(p => p.PitchId).ToList());
}

public sealed class PartnerCodeRequestValidator : AbstractValidator<PartnerCodeRequest>
{
    public PartnerCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3, 20).Matches("^[A-Za-z0-9]+$").WithMessage("3–20 letters and numbers.");
        RuleFor(x => x.DiscountType).Must(t => t is not null && (t.Equals("Percent", StringComparison.OrdinalIgnoreCase) || t.Equals("Fixed", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Percent or Fixed.");
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => string.Equals(x.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.MinSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses is not null);
        RuleFor(x => x.PerCustomer).GreaterThan(0).When(x => x.PerCustomer is not null);
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn).When(x => x.EndsOn is not null);
        RuleFor(x => x.Label).MaximumLength(60);
    }
}
