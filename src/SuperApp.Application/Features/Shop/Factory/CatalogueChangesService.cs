using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>
/// Catalogue changes asked by the factory (FR-POD-12): price per piece, colours, sizes, pausing a product, details.
/// They wait for an admin (approvals.catalogue); only approved changes reach seller pages, and sellers using the product are told.
/// </summary>
public interface ICatalogueChangesService
{
    Task<CatalogueChangeDto> RequestAsync(CatalogueChangeRequest request, CancellationToken ct);

    Task<IReadOnlyList<CatalogueChangeDto>> MineAsync(CancellationToken ct);

    Task<IReadOnlyList<CatalogueChangeDto>> PendingAsync(CancellationToken ct);

    Task ApproveAsync(long id, CancellationToken ct);

    Task RejectAsync(long id, ResolveRequest request, CancellationToken ct);
}

public sealed class CatalogueChangesService(
    IFactoryAccess access,
    IFactoryRepository factories,
    IFactoryAdminRepository admin,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<CatalogueChangeRequest> rules) : ICatalogueChangesService
{
    public async Task<CatalogueChangeDto> RequestAsync(CatalogueChangeRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var me = (await access.CurrentAsync(ct)).Need("settings");
        var kind = Enum.Parse<CatalogChangeKind>(request.Kind, true);
        _ = await factories.CatalogProductAsync(request.CatalogProductId, ct) ?? throw new NotFoundException("Product not found.");
        CatalogueChanges.Check(kind, request.Change);
        if (await factories.PendingChangeAsync(me.FactoryId, request.CatalogProductId, kind, ct))
        {
            throw new ConflictException("You already asked for this kind of change on this product. Wait for the answer.", "pending_change");
        }
        var change = new CatalogChangeRequest
        {
            CatalogProductId = request.CatalogProductId, FactoryId = me.FactoryId, RequestedByStaffId = me.StaffId, Kind = kind,
            ChangeJson = request.Change.GetRawText(), Reason = request.Reason.Trim(), Status = ChangeRequestStatus.Pending,
        };
        factories.AddChange(change);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("approvals.catalogue", new NotificationMessage(NotificationCategory.System, "Catalogue change to approve",
            $"{me.FactoryName}: {kind} · {request.Reason.Trim()}", "#/admin/approvals"), ct);
        return (await factories.ChangesAsync(me.FactoryId, false, ct)).Single(c => c.Id == change.Id);
    }

    public async Task<IReadOnlyList<CatalogueChangeDto>> MineAsync(CancellationToken ct) => await factories.ChangesAsync((await access.CurrentAsync(ct)).FactoryId, false, ct);

    public Task<IReadOnlyList<CatalogueChangeDto>> PendingAsync(CancellationToken ct) => factories.ChangesAsync(null, true, ct);

    public async Task ApproveAsync(long id, CancellationToken ct)
    {
        var change = await PendingChangeAsync(id, ct);
        var product = await admin.CatalogForEditAsync(change.CatalogProductId, ct) ?? throw new NotFoundException();
        using var json = JsonDocument.Parse(change.ChangeJson);
        await ApplyAsync(change, product, json.RootElement, ct);
        (change.Status, change.ReviewedById, change.ReviewedAt) = (ChangeRequestStatus.Approved, currentUser.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(change.Factory.OwnerUserId, new NotificationMessage(NotificationCategory.System, "Catalogue change approved", $"{change.Kind} · {product.NameEn}", "#/factory", NotificationTone.Success), ct);
        foreach (var seller in await admin.SellersUsingAsync(product.Id, ct))
        {
            await notifier.NotifyUserAsync(seller, new NotificationMessage(NotificationCategory.Designs, $"{product.NameEn} changed", $"{change.Kind}: {change.Reason}", "#/seller/catalogue"), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "catalogue.change_approved", $"Approved {change.Kind} on {product.NameEn}", "CatalogChangeRequest", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    public async Task RejectAsync(long id, ResolveRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Resolution) || request.Resolution.Trim().Length is < 3 or > 400)
        {
            throw new FluentValidation.ValidationException([new FluentValidation.Results.ValidationFailure("resolution", "Say why (3–400 characters).")]);
        }
        var change = await PendingChangeAsync(id, ct);
        (change.Status, change.ReviewedById, change.ReviewedAt, change.ReviewNote) = (ChangeRequestStatus.Rejected, currentUser.UserId, clock.UtcNow, request.Resolution.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(change.Factory.OwnerUserId, new NotificationMessage(NotificationCategory.System, "Catalogue change not approved", change.ReviewNote, "#/factory", NotificationTone.Warning), ct);
    }

    private async Task<CatalogChangeRequest> PendingChangeAsync(long id, CancellationToken ct)
    {
        var change = await admin.ChangeAsync(id, ct) ?? throw new NotFoundException("Change request not found.");
        return change.Status == ChangeRequestStatus.Pending ? change : throw new BusinessRuleException("This request is already decided.", "wrong_status");
    }

    private async Task ApplyAsync(CatalogChangeRequest change, CatalogProduct product, JsonElement c, CancellationToken ct)
    {
        switch (change.Kind)
        {
            case CatalogChangeKind.Price:
                var price = c.GetProperty("pricePerPiece").GetDecimal();
                if (price >= product.BasePrice) { throw new BusinessRuleException("The factory price must stay below the base price.", "cost_over_base"); }
                product.FactoryCost = price;
                var row = await admin.PriceAsync(change.FactoryId, product.Id, ct);
                if (row is null) { admin.AddPrice(new FactoryPrice { FactoryId = change.FactoryId, CatalogProductId = product.Id, PricePerPiece = price }); }
                else { row.PricePerPiece = price; }
                break;
            case CatalogChangeKind.PauseProduct:
                product.Status = c.GetProperty("paused").GetBoolean() ? CatalogProductStatus.Paused : CatalogProductStatus.Active;
                break;
            default:
                CatalogueChanges.ApplyListChange(change.Kind, product, c);
                break;
        }
    }
}

/// <summary>Checks and applies the JSON of a catalogue change.</summary>
public static class CatalogueChanges
{
    public static void Check(CatalogChangeKind kind, JsonElement c)
    {
        var ok = c.ValueKind == JsonValueKind.Object && kind switch
        {
            CatalogChangeKind.Price => c.TryGetProperty("pricePerPiece", out var p) && p.ValueKind == JsonValueKind.Number && p.GetDecimal() > 0,
            CatalogChangeKind.PauseProduct => c.TryGetProperty("paused", out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False,
            CatalogChangeKind.Colour => (c.TryGetProperty("colorId", out var id) && id.ValueKind == JsonValueKind.Number && c.TryGetProperty("active", out _))
                || (c.TryGetProperty("add", out var a) && Has(a, "nameEn", "nameAr", "hex")),
            CatalogChangeKind.Size => c.TryGetProperty("add", out var s) && Has(s, "size") && s.TryGetProperty("chestCm", out _) && s.TryGetProperty("lengthCm", out _) && s.TryGetProperty("sleeveCm", out _),
            _ => c.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Array && d.GetArrayLength() is > 0 and <= 10 && d.EnumerateArray().All(x => Has(x, "labelEn", "labelAr", "valueEn", "valueAr")),
        };
        if (!ok)
        {
            throw new BadRequestException("The change doesn’t match its kind. See the API notes for the format.", "bad_change");
        }
    }

    public static void ApplyListChange(CatalogChangeKind kind, CatalogProduct product, JsonElement c)
    {
        if (kind == CatalogChangeKind.Colour && c.TryGetProperty("colorId", out var id))
        {
            var color = product.Colors.FirstOrDefault(x => x.Id == id.GetInt64()) ?? throw new NotFoundException("Colour not found.");
            color.IsActive = c.GetProperty("active").GetBoolean();
        }
        else if (kind == CatalogChangeKind.Colour)
        {
            var a = c.GetProperty("add");
            product.Colors.Add(new CatalogColor { NameEn = a.GetProperty("nameEn").GetString()!, NameAr = a.GetProperty("nameAr").GetString()!, Hex = a.GetProperty("hex").GetString()!, SortOrder = product.Colors.Count + 1 });
        }
        else if (kind == CatalogChangeKind.Size)
        {
            var s = c.GetProperty("add");
            product.Sizes.Add(new CatalogSize
            {
                Size = s.GetProperty("size").GetString()!, ChestCm = s.GetProperty("chestCm").GetDecimal(), LengthCm = s.GetProperty("lengthCm").GetDecimal(),
                SleeveCm = s.GetProperty("sleeveCm").GetDecimal(), IsBigSize = s.TryGetProperty("isBigSize", out var big) && big.ValueKind == JsonValueKind.True, SortOrder = product.Sizes.Count + 1,
            });
        }
        else
        {
            product.Details.Clear();
            foreach (var (d, i) in c.GetProperty("details").EnumerateArray().Select((d, i) => (d, i)))
            {
                product.Details.Add(new CatalogDetail { LabelEn = d.GetProperty("labelEn").GetString()!, LabelAr = d.GetProperty("labelAr").GetString()!, ValueEn = d.GetProperty("valueEn").GetString()!, ValueAr = d.GetProperty("valueAr").GetString()!, SortOrder = i + 1 });
            }
        }
    }

    private static bool Has(JsonElement e, params string[] names) =>
        e.ValueKind == JsonValueKind.Object && names.All(n => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()));
}

public sealed class CatalogueChangeRequestValidator : AbstractValidator<CatalogueChangeRequest>
{
    public CatalogueChangeRequestValidator()
    {
        RuleFor(x => x.CatalogProductId).GreaterThan(0);
        RuleFor(x => x.Kind).IsEnumName(typeof(CatalogChangeKind), caseSensitive: false).WithMessage("Price, Colour, Size, PauseProduct or Details.");
        RuleFor(x => x.Reason).NotEmpty().Length(3, 400);
    }
}
