using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>Every league is free (owner, 2026-11-03): the Fantasy Pass is no longer sold. Payments already started
/// before the change are still settled when the gateway calls back: its 30 days start now (or when the current pass ends).</summary>
public sealed class FantasyPassPaymentListener(IFantasyPassRepository passes, Money.ILedger ledger, IUnitOfWork unitOfWork, IClock clock) : Payments.IPaymentListener
{
    public Domain.Enums.PaymentPurpose Purpose => Domain.Enums.PaymentPurpose.FantasyPass;

    public async Task PaidAsync(Domain.Entities.Money.Payment payment, CancellationToken ct)
    {
        var pass = await passes.ByPaymentAsync(payment.Id, ct);
        if (pass is null || pass.EndsAt > clock.UtcNow)
        {
            return;
        }
        var now = clock.UtcNow;
        var current = (await passes.ActiveAsync(pass.UserId, now, ct)).Where(p => p.CompetitionId == pass.CompetitionId && p.Id != pass.Id).Select(p => p.EndsAt).DefaultIfEmpty(now).Max();
        pass.StartsAt = current > now ? current : now;
        pass.EndsAt = pass.StartsAt.AddDays(30);
        await ledger.PlatformSaleAsync(payment, "FantasyPass", pass.Id, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task FailedAsync(Domain.Entities.Money.Payment payment, CancellationToken ct) => Task.CompletedTask;
}
