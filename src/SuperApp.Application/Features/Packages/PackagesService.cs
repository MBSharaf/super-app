using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Packages;

/// <summary>
/// Weekly packages (API §3.5): the same hour every week for 1–12 matches with the approved discount and one QR for all
/// of them. Weeks already taken are skipped (up to 4 extra weeks). Paid straight away until payments arrive (Phase 5).
/// </summary>
public interface IPackagesService
{
    Task<PackageQuoteDto> QuoteAsync(PackageQuoteQuery query, CancellationToken ct);

    Task<PackageDto> BuyAsync(BuyPackageRequest request, CancellationToken ct);

    Task<IReadOnlyList<PackageDto>> MineAsync(CancellationToken ct);

    Task<PackageDto> GetAsync(string code, CancellationToken ct);

    Task<byte[]> QrPngAsync(string code, CancellationToken ct);
}

public sealed class PackagesService(
    IPackageRepository packages,
    IBookingRepository bookings,
    BookingPlanner planner,
    ILegalRepository legal,
    ISettingsReader settings,
    IQrTokens qr,
    IFeatureFlags flags,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IValidator<PackageQuoteQuery> quoteRules,
    IValidator<BuyPackageRequest> buyRules,
    Payments.IPaymentsService payments,
    Money.ILedger ledger,
    IUnitOfWork unitOfWork) : IPackagesService
{
    public const int ExtraWeeks = 4;

    public async Task<PackageQuoteDto> QuoteAsync(PackageQuoteQuery query, CancellationToken ct)
    {
        await quoteRules.ValidateAndThrowAsync(query, ct);
        return (await PlanAsync(query.PitchSlug, query.OptionId, query.StartsOn, query.Hour, query.Duration, ct)).Quote;
    }

    public async Task<PackageDto> BuyAsync(BuyPackageRequest request, CancellationToken ct)
    {
        await buyRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Bookings, ct);
        var me = Me();
        // A repeated request (double tap, retry) gets the package it already bought, before any week is checked
        var option = await packages.ApprovedOptionAsync(request.PitchSlug, request.OptionId, ct);
        if (option is not null && await packages.RecentSameAsync(me, option.PitchId, request.StartsOn, request.Hour % 24, ct) is { } repeated)
        {
            return await GetAsync(repeated.Code, ct);
        }
        var plan = await PlanAsync(request.PitchSlug, request.OptionId, request.StartsOn, request.Hour, request.Duration, ct);
        var first = plan.Plans[0];
        var package = await NewPackageAsync(plan, request, me, ct);
        var waiver = await legal.CurrentVersionIdAsync("waiver", ct);
        var matches = new List<(Booking, IReadOnlyList<(DateOnly, int)>)>();
        foreach (var p in plan.Plans)
        {
            matches.Add((await MatchAsync(p, package, plan, request.Duration, waiver, me, ct), p.Slots));
        }
        var paymentUrl = await PayAsync(package, matches.Select(m => m.Item1).ToList(), first, me, request.PayFromWallet, ct);
        await packages.SaveAsync(package, matches, ct);
        if (paymentUrl is not null)
        {
            return (await GetAsync(package.Code, ct)) with { PaymentUrl = paymentUrl };
        }
        foreach (var (match, _) in matches)
        {
            await ledger.BookingPaidAsync(match, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await AfterBuyAsync(package, first, waiver, me, ct);
        return await GetAsync(package.Code, ct);
    }

    /// <summary>One payment for the whole package, shared by its matches; with Paymob the matches wait for it (AwaitingPayment).</summary>
    private async Task<string?> PayAsync(Package package, IReadOnlyList<Booking> matches, BookingPlan first, Guid me, bool fromWallet, CancellationToken ct)
    {
        var started = await payments.StartAsync(new Payments.PaymentRequest(PaymentPurpose.Package, package.TotalPrice, me,
            $"Package {package.Code} at {first.Pitch.Name}", $"#/package/{package.Code}", fromWallet), ct);
        package.Payment = started.Payment;
        foreach (var m in matches)
        {
            m.Payment = started.Payment;
            m.Status = started.RedirectUrl is null ? BookingStatus.Confirmed : BookingStatus.AwaitingPayment;
        }
        return started.RedirectUrl;
    }

    public async Task<IReadOnlyList<PackageDto>> MineAsync(CancellationToken ct) => await packages.MineAsync(Me(), ct);

    public async Task<PackageDto> GetAsync(string code, CancellationToken ct)
    {
        var p = await packages.GetAsync(Me(), code, ct) ?? throw new NotFoundException("This package does not exist.");
        return p with { QrToken = p.Status == nameof(PackageStatus.Active) ? qr.Create(p.Code) : null };
    }

    public async Task<byte[]> QrPngAsync(string code, CancellationToken ct) => qr.Png(qr.Create((await GetAsync(code, ct)).Code));

    private sealed record PackagePlan(PackageQuoteDto Quote, IReadOnlyList<BookingPlan> Plans, decimal FeeRate);

    /// <summary>Plans every week with the booking rules; a week that can't be booked is skipped.</summary>
    private async Task<PackagePlan> PlanAsync(string slug, long optionId, DateOnly startsOn, int hour, int duration, CancellationToken ct)
    {
        var option = await packages.ApprovedOptionAsync(slug, optionId, ct) ?? throw new BusinessRuleException("This pitch doesn’t offer this package size.", "size_not_offered");
        var discount = option.ApprovedDiscount ?? option.DiscountPercent;
        var feeRate = await settings.DecimalAsync("fees.service", 0.03m, ct);
        var plans = new List<BookingPlan>();
        var weeks = new List<QuoteMatch>();
        for (var k = 0; plans.Count < option.MatchCount && k < option.MatchCount + ExtraWeeks; k++)
        {
            var date = startsOn.AddDays(7 * k);
            try
            {
                var p = await planner.PlanAsync(slug, date, hour, duration, ct);
                plans.Add(p);
                weeks.Add(new QuoteMatch(date, hour, true, p.PitchAmount, Discounted(p.PitchAmount, discount)));
            }
            catch (AppException ex) when (ex is BusinessRuleException or ConflictException)
            {
                weeks.Add(new QuoteMatch(date, hour, false, 0, 0));
            }
        }
        if (plans.Count < option.MatchCount)
        {
            throw new ConflictException($"Only {plans.Count} of the {option.MatchCount} weeks are free at this time. Choose another hour or day.", "not_enough_weeks");
        }
        var list = plans.Sum(p => p.PitchAmount);
        var price = plans.Sum(p => Discounted(p.PitchAmount, discount));
        var fee = plans.Sum(p => Fee(Discounted(p.PitchAmount, discount), feeRate));
        var quote = new PackageQuoteDto(option.Id, option.MatchCount, discount, weeks, weeks.Count(w => !w.Available), list, list - price, fee, price + fee);
        return new PackagePlan(quote, plans, feeRate);
    }

    private async Task<Package> NewPackageAsync(PackagePlan plan, BuyPackageRequest request, Guid me, CancellationToken ct)
    {
        var first = plan.Plans[0];
        return new Package
        {
            Code = await packages.NewCodeAsync(ct), PitchId = first.Pitch.Id, CustomerId = me, PackageOptionId = plan.Quote.OptionId,
            MatchCount = (byte)plan.Quote.MatchCount, DayOfWeek = (byte)request.StartsOn.DayOfWeek, StartHour = (byte)first.Lines[0].CalendarHour,
            Duration = (byte)request.Duration, StartsOn = request.StartsOn, EndsOn = plan.Plans[^1].Lines[0].Date,
            DiscountPercent = plan.Quote.DiscountPercent, TotalPrice = plan.Quote.Total, Status = PackageStatus.Active,
        };
    }

    private async Task<Booking> MatchAsync(BookingPlan p, Package package, PackagePlan plan, int duration, long? waiver, Guid me, CancellationToken ct)
    {
        var amount = Discounted(p.PitchAmount, plan.Quote.DiscountPercent);
        var fee = Fee(amount, plan.FeeRate);
        var commission = p.Pitch.PartnerCommission ?? (await settings.DecimalAsync("commission.bookings", 0.10m, ct) * 100);
        return new Booking
        {
            Code = await bookings.NewCodeAsync(ct), PitchId = p.Pitch.Id, CustomerId = me, Source = BookingSource.Package, Package = package,
            Date = p.Lines[0].Date, StartHour = (byte)p.Lines[0].CalendarHour, Duration = (byte)duration, PitchAmount = amount,
            DiscountAmount = p.PitchAmount - amount, ServiceFee = fee, Total = amount + fee, CommissionRate = commission,
            PolicyKey = p.Pitch.PolicyKey, WaiverVersionId = waiver, Status = BookingStatus.Confirmed,
        };
    }

    private async Task AfterBuyAsync(Package package, BookingPlan first, long? waiver, Guid me, CancellationToken ct)
    {
        if (waiver is { } w)
        {
            legal.AddAcceptance(w, me, "package", requestInfo.IpAddress);
        }
        var when = $"{package.MatchCount} matches · every {package.StartsOn.DayOfWeek} {first.Lines[0].Hour % 24:00}:00";
        await notifier.NotifyUserAsync(me, new NotificationMessage(NotificationCategory.Bookings, $"Package booked: {first.Pitch.Name}",
            $"{when} · {package.Code} · one QR for every match", $"#/package/{package.Code}", NotificationTone.Success), ct);
        await notifier.NotifyUserAsync(first.Pitch.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings, $"New package at {first.Pitch.Name}",
            $"{when} · {package.TotalPrice:0} EGP", "#/dash/bookings"), ct);
        await audit.LogAsync(AuditSystem.Site, "package.created", $"Package {package.Code} at {first.Pitch.Name}: {package.MatchCount} matches", "Package", package.Code, ct: ct);
    }

    private static decimal Discounted(decimal amount, decimal discount) => Math.Round(amount * (100 - discount) / 100, 0, MidpointRounding.AwayFromZero);

    private static decimal Fee(decimal amount, decimal rate) => Math.Round(amount * rate, 0, MidpointRounding.AwayFromZero);

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class PackageQuoteQueryValidator : AbstractValidator<PackageQuoteQuery>
{
    public PackageQuoteQueryValidator()
    {
        RuleFor(x => x.PitchSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.OptionId).GreaterThan(0);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.Duration).InclusiveBetween(1, 4).WithMessage("Book 1 to 4 hours in a row.");
    }
}

public sealed class BuyPackageRequestValidator : AbstractValidator<BuyPackageRequest>
{
    public BuyPackageRequestValidator()
    {
        RuleFor(x => x.PitchSlug).NotEmpty().MaximumLength(80);
        RuleFor(x => x.OptionId).GreaterThan(0);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.Duration).InclusiveBetween(1, 4).WithMessage("Book 1 to 4 hours in a row.");
        RuleFor(x => x.AcceptWaiver).Equal(true).WithMessage("Please accept the injury waiver to book.");
    }
}
