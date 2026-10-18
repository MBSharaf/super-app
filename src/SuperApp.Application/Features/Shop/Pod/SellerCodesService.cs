using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>A seller's store discount codes (API §4.4 /seller/codes): paid by the store, only its items; active codes limited by level.</summary>
public interface ISellerCodesService
{
    Task<IReadOnlyList<SellerCodeDto>> ListAsync(CancellationToken ct);

    Task<SellerCodeDto> CreateAsync(SellerCodeRequest request, CancellationToken ct);

    Task<SellerCodeDto> UpdateAsync(long id, SellerCodeRequest request, CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

public sealed class SellerCodesService(ISellerAccess access, ISellerInsightsRepository codes, IUnitOfWork unitOfWork, IValidator<SellerCodeRequest> rules) : ISellerCodesService
{
    public async Task<IReadOnlyList<SellerCodeDto>> ListAsync(CancellationToken ct) => await codes.CodesAsync((await access.CurrentAsync(ct)).StoreId, ct);

    public async Task<SellerCodeDto> CreateAsync(SellerCodeRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = await access.CurrentAsync(ct);
        var code = new PromoCode { Funding = CodeFunding.OwnerFunded, OwnerUserId = me.UserId, StoreId = me.StoreId, Scope = CodeScope.Shop };
        await ApplyAsync(me, code, request, null, ct);
        codes.AddCode(code);
        await unitOfWork.SaveChangesAsync(ct);
        return (await codes.CodesAsync(me.StoreId, ct)).Single(c => c.Id == code.Id);
    }

    public async Task<SellerCodeDto> UpdateAsync(long id, SellerCodeRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = await access.CurrentAsync(ct);
        var code = await codes.CodeAsync(me.StoreId, id, ct) ?? throw new NotFoundException("Code not found.");
        await ApplyAsync(me, code, request, id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return (await codes.CodesAsync(me.StoreId, ct)).Single(c => c.Id == id);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var code = await codes.CodeAsync(me.StoreId, id, ct) ?? throw new NotFoundException("Code not found.");
        code.IsDeleted = true;
        code.DeletedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(SellerScope me, PromoCode code, SellerCodeRequest r, long? id, CancellationToken ct)
    {
        var text = r.Code.Trim().ToUpperInvariant();
        if (await codes.CodeTakenAsync(text, id, ct))
        {
            throw new ConflictException("This code is taken. Try another.", "code_taken");
        }
        if (r.Active && me.Level.ActiveCodes is { } limit && await codes.ActiveCodesAsync(me.StoreId, id, ct) >= limit)
        {
            throw new BusinessRuleException($"Your level allows {limit} active codes. Pause one first.", "code_limit");
        }
        (code.Code, code.DiscountType, code.Value, code.MinSpend, code.MaxUses, code.PerCustomer) =
            (text, Enum.Parse<DiscountType>(r.DiscountType, true), r.Value, r.MinSpend, r.MaxUses, r.PerCustomer);
        (code.StartsOn, code.EndsOn, code.Label, code.Status) = (r.StartsOn, r.EndsOn, r.Label?.Trim(), r.Active ? CodeStatus.Active : CodeStatus.Paused);
    }
}

public sealed class SellerCodeRequestValidator : AbstractValidator<SellerCodeRequest>
{
    public SellerCodeRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3, 30).Matches("^[A-Za-z0-9]+$").WithMessage("Letters and numbers only.");
        RuleFor(x => x.DiscountType).IsEnumName(typeof(DiscountType), caseSensitive: false).WithMessage("Percent, Fixed or FreeDelivery.");
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Value).InclusiveBetween(1, 90).When(x => string.Equals(x.DiscountType, "Percent", StringComparison.OrdinalIgnoreCase)).WithMessage("A percentage between 1 and 90.");
        RuleFor(x => x.Value).GreaterThan(0).When(x => string.Equals(x.DiscountType, "Fixed", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.MinSpend).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxUses).GreaterThan(0).When(x => x.MaxUses is not null);
        RuleFor(x => x.PerCustomer).GreaterThan(0).When(x => x.PerCustomer is not null);
        RuleFor(x => x.EndsOn).GreaterThanOrEqualTo(x => x.StartsOn).When(x => x.EndsOn is not null).WithMessage("The end date is after the start.");
        RuleFor(x => x.Label).MaximumLength(120);
    }
}
