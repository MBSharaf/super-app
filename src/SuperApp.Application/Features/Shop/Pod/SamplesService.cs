using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>
/// Samples (FR-POD-09): a seller orders their own design to check the print. Price by level (base; Pro −30%; Elite one free
/// a month), paid from the available balance. Until the money phase the sample is approved at once and becomes a paid
/// order with a factory job marked as a sample.
/// </summary>
public interface ISamplesService
{
    Task<IReadOnlyList<SampleDto>> ListAsync(CancellationToken ct);

    Task<SampleDto> OrderAsync(SampleRequest request, CancellationToken ct);
}

public sealed class SamplesService(
    ISellerAccess access,
    IDesignRepository designs,
    ISellerInsightsRepository insights,
    IOrderRepository orders,
    ISettingsReader settings,
    INotifier notifier,
    IClock clock,
    IValidator<SampleRequest> rules) : ISamplesService
{
    public async Task<IReadOnlyList<SampleDto>> ListAsync(CancellationToken ct) => await insights.SamplesAsync((await access.CurrentAsync(ct)).SellerId, ct);

    public async Task<SampleDto> OrderAsync(SampleRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = await access.CurrentAsync(ct);
        var design = await designs.DesignAsync(me.SellerId, request.DesignId, ct);
        if (design is null || design.Status == DesignStatus.Removed || (design.FrontArtFileId is null && design.BackArtFileId is null))
        {
            throw new NotFoundException("Choose one of your designs with art.");
        }
        var product = await designs.CatalogProductAsync(design.CatalogProductId, ct) ?? throw new BusinessRuleException("This product is no longer printed.", "unknown_product");
        var color = product.Colors.FirstOrDefault(c => c.IsActive && string.Equals(c.NameEn, request.Color.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Choose one of the product’s colours.", "bad_color");
        var size = product.Sizes.FirstOrDefault(s => string.Equals(s.Size, request.Size.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("Choose one of the product’s sizes.", "bad_size");
        var factory = await orders.AcceptingFactoryAsync(ct) ?? throw new ConflictException("Printing is paused for a few days. Try again later.", "printing_paused");
        var (price, free) = await PriceAsync(me, product, design, ct);
        var balance = (await insights.KpisAsync(me.SellerId, me.StoreId, (int)await settings.DecimalAsync("pod.holdDays", 7, ct), ct)).AvailableEarnings;
        if (price > balance)
        {
            throw new BusinessRuleException($"The sample costs {price:0} EGP and your available balance is {balance:0} EGP.", "balance_too_low");
        }
        var order = await SampleOrderAsync(me, design, product, color.NameEn, size.Size, request.Address.Trim(), price, ct);
        var sample = new Sample { SellerId = me.SellerId, DesignId = design.Id, Color = color.NameEn, Size = size.Size, Address = request.Address.Trim(), Price = price, IsFree = free, Order = order, Status = SampleStatus.Approved };
        insights.AddSample(sample);
        var job = new Job { Code = await orders.NewJobCodeAsync(ct), OrderItem = order.Items.Single(), FactoryId = factory.Id, DueAt = clock.UtcNow.AddHours(factory.SlaHours), IsSample = true };
        await orders.PlaceAsync(order, [job], [], null, [], ct);
        await notifier.NotifyUserAsync(factory.OwnerUserId, new NotificationMessage(NotificationCategory.Orders, "New sample to print", $"{design.Title} · {order.Code}", "#/factory"), ct);
        return (await insights.SamplesAsync(me.SellerId, ct)).First(s => s.Id == sample.Id);
    }

    private async Task<(decimal Price, bool Free)> PriceAsync(SellerScope me, CatalogProduct product, Design design, CancellationToken ct)
    {
        var basePrice = DesignRules.Base(product, design.BackArtFileId is not null && product.HasBackPrint, me.Level.BaseDiscount);
        if (me.Level.FreeSamplesPerMonth > 0)
        {
            var monthStart = new DateTime(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            if (await insights.FreeSamplesSinceAsync(me.SellerId, monthStart, ct) < me.Level.FreeSamplesPerMonth)
            {
                return (0, true);
            }
            return (basePrice, false);
        }
        return (Math.Round(basePrice * (1 - Math.Min(me.Level.SampleDiscount, 1)), 0, MidpointRounding.AwayFromZero), false);
    }

    private async Task<Order> SampleOrderAsync(SellerScope me, Design design, CatalogProduct product, string color, string size, string address, decimal price, CancellationToken ct)
    {
        var order = new Order
        {
            Code = await orders.NewCodeAsync(ct), CustomerId = me.UserId, AddressJson = JsonSerializer.Serialize(new { label = "Sample", line1 = address, city = "" }),
            Phone = "-", Subtotal = price, Total = price, PaymentMethod = OrderPaymentMethod.Wallet, Status = OrderStatus.Paid,
        };
        order.Items.Add(new OrderItem
        {
            Kind = OrderItemKind.PrintedDesign, StoreId = me.StoreId, DesignId = design.Id, Title = $"Sample — {design.Title}".Length > 150 ? design.Title : $"Sample — {design.Title}",
            Size = size, Color = color, Qty = 1, UnitPrice = price, BaseAmount = price, CommissionAmount = Math.Max(0, price - product.FactoryCost), Status = OrderItemStatus.Received,
        });
        return order;
    }
}

public sealed class SampleRequestValidator : AbstractValidator<SampleRequest>
{
    public SampleRequestValidator()
    {
        RuleFor(x => x.DesignId).GreaterThan(0);
        RuleFor(x => x.Color).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Size).NotEmpty().MaximumLength(5);
        RuleFor(x => x.Address).NotEmpty().Length(10, 400);
    }
}
