using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Stores;

/// <summary>
/// A store's own products (API §4.5, §4.4 "own products"): add, edit (variants with stock, specs), delete, restock and photos.
/// The same service serves the partner's shop, a seller's store and the official store — the scope says which.
/// </summary>
public interface IStoreProductsService
{
    Task<IReadOnlyList<OwnProductDto>> ListAsync(StoreScope scope, CancellationToken ct);

    Task<OwnProductDto> GetAsync(StoreScope scope, long id, CancellationToken ct);

    Task<OwnProductDto> CreateAsync(StoreScope scope, ProductRequest request, CancellationToken ct);

    Task<OwnProductDto> UpdateAsync(StoreScope scope, long id, ProductRequest request, CancellationToken ct);

    Task DeleteAsync(StoreScope scope, long id, CancellationToken ct);

    Task<OwnProductDto> RestockAsync(StoreScope scope, long id, RestockRequest request, CancellationToken ct);

    Task<OwnProductDto> AddImagesAsync(StoreScope scope, long id, IReadOnlyList<UploadedFile> uploads, CancellationToken ct);

    Task<OwnProductDto> DeleteImageAsync(StoreScope scope, long id, long imageId, CancellationToken ct);

    Task<OwnProductDto> OrderImagesAsync(StoreScope scope, long id, ImageOrderRequest request, CancellationToken ct);
}

public sealed class StoreProductsService(
    IStoreRepository stores,
    IFileStorage files,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IValidator<ProductRequest> rules) : IStoreProductsService
{
    public const int MaxImages = 8;

    public Task<IReadOnlyList<OwnProductDto>> ListAsync(StoreScope scope, CancellationToken ct) => stores.ProductsAsync(scope.StoreId, ct);

    public async Task<OwnProductDto> GetAsync(StoreScope scope, long id, CancellationToken ct) =>
        await stores.ProductDtoAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");

    public async Task<OwnProductDto> CreateAsync(StoreScope scope, ProductRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var product = new Product { StoreId = scope.StoreId, Slug = await stores.FreeSlugAsync(request.Name, ct) };
        await ApplyAsync(product, request, ct);
        stores.AddProduct(product);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(scope.System, "product.created", $"{scope.Who} added “{product.Name}”", "Product", product.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return await GetAsync(scope, product.Id, ct);
    }

    public async Task<OwnProductDto> UpdateAsync(StoreScope scope, long id, ProductRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        await ApplyAsync(product, request, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(scope.System, "product.updated", $"{scope.Who} edited “{product.Name}”", "Product", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
        return await GetAsync(scope, id, ct);
    }

    public async Task DeleteAsync(StoreScope scope, long id, CancellationToken ct)
    {
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        product.IsDeleted = true;
        product.DeletedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(scope.System, "product.deleted", $"{scope.Who} deleted “{product.Name}”", "Product", id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    public async Task<OwnProductDto> RestockAsync(StoreScope scope, long id, RestockRequest request, CancellationToken ct)
    {
        if (request.Add is < 1 or > 10_000)
        {
            throw new BadRequestException("Add between 1 and 10,000 pieces.", "bad_quantity");
        }
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        var variant = product.Variants.FirstOrDefault(v => v.Id == request.VariantId) ?? throw new NotFoundException("This size or colour is not part of the product.");
        variant.Stock += request.Add;
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    public async Task<OwnProductDto> AddImagesAsync(StoreScope scope, long id, IReadOnlyList<UploadedFile> uploads, CancellationToken ct)
    {
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        if (uploads.Count == 0 || product.Images.Count + uploads.Count > MaxImages)
        {
            throw new BusinessRuleException($"A product can have 1 to {MaxImages} photos.", "too_many_photos");
        }
        var next = product.Images.Count == 0 ? 1 : product.Images.Max(i => i.SortOrder) + 1;
        foreach (var file in uploads)
        {
            product.Images.Add(new ProductImage { FileId = await files.SaveAsync(file, UploadKind.Photo, currentUser.UserId ?? Guid.Empty, "photos", ct), SortOrder = next++ });
        }
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    public async Task<OwnProductDto> DeleteImageAsync(StoreScope scope, long id, long imageId, CancellationToken ct)
    {
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        var image = product.Images.FirstOrDefault(i => i.Id == imageId) ?? throw new NotFoundException("Photo not found.");
        if (product.Status == ProductStatus.Published && product.Images.Count == 1)
        {
            throw new BusinessRuleException("A product on sale needs at least one photo.", "last_photo");
        }
        product.Images.Remove(image);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    public async Task<OwnProductDto> OrderImagesAsync(StoreScope scope, long id, ImageOrderRequest request, CancellationToken ct)
    {
        var product = await stores.ProductAsync(scope.StoreId, id, ct) ?? throw new NotFoundException("Product not found.");
        if (request.ImageIds.Count != product.Images.Count || request.ImageIds.Distinct().Count() != product.Images.Count || product.Images.Any(i => !request.ImageIds.Contains(i.Id)))
        {
            throw new BadRequestException("Send every photo of the product once.", "bad_order");
        }
        foreach (var image in product.Images)
        {
            image.SortOrder = request.ImageIds.ToList().IndexOf(image.Id) + 1;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(scope, id, ct);
    }

    private async Task ApplyAsync(Product product, ProductRequest r, CancellationToken ct)
    {
        product.CategoryId = await stores.CategoryIdAsync(r.CategoryKey, ct) ?? throw new BadRequestException("Choose a category from the list.", "unknown_category");
        product.Name = r.Name.Trim();
        product.Brand = string.IsNullOrWhiteSpace(r.Brand) ? null : r.Brand.Trim();
        product.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        product.Price = r.Price;
        product.CompareAtPrice = r.CompareAtPrice;
        product.LowStockAt = r.LowStockAt;
        product.Status = Enum.Parse<ProductStatus>(r.Status, true);
        product.Specs.Clear();
        foreach (var (spec, i) in r.Specs.Select((s, i) => (s, i)))
        {
            product.Specs.Add(new ProductSpec { Label = spec.Label.Trim(), Value = spec.Value.Trim(), SortOrder = i + 1 });
        }
        await ApplyVariantsAsync(product, r.Variants, ct);
    }

    /// <summary>Variants with an id are changed, new ones added, missing ones removed (kept with no stock when an order refers to them).</summary>
    private async Task ApplyVariantsAsync(Product product, IReadOnlyList<VariantRequest> variants, CancellationToken ct)
    {
        foreach (var gone in product.Variants.Where(v => variants.All(x => x.Id != v.Id)).ToList())
        {
            if (gone.Id != 0 && await stores.VariantInUseAsync(gone.Id, ct)) { gone.Stock = gone.Reserved; }
            else { product.Variants.Remove(gone); stores.RemoveVariant(gone); }
        }
        foreach (var v in variants)
        {
            var variant = v.Id is { } id ? product.Variants.FirstOrDefault(x => x.Id == id) ?? throw new NotFoundException("This size or colour is not part of the product.") : new ProductVariant();
            var sku = string.IsNullOrWhiteSpace(v.Sku) ? (variant.Id == 0 ? await stores.NewSkuAsync(product.Slug, ct) : variant.Sku) : v.Sku.Trim().ToUpperInvariant();
            if (await stores.SkuTakenAsync(sku, variant.Id == 0 ? null : variant.Id, ct) || product.Variants.Any(x => x != variant && x.Sku == sku))
            {
                throw new ConflictException($"The SKU {sku} is already used by another product.", "sku_taken");
            }
            (variant.Size, variant.ColorName, variant.ColorHex, variant.Sku, variant.Stock, variant.PriceOverride) =
                (v.Size.Trim(), v.ColorName.Trim(), v.ColorHex, sku, Math.Max(v.Stock, variant.Reserved), v.PriceOverride);
            if (variant.Id == 0 && !product.Variants.Contains(variant)) { product.Variants.Add(variant); }
        }
    }
}

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 150);
        RuleFor(x => x.CategoryKey).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Brand).MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.CompareAtPrice).GreaterThan(x => x.Price).When(x => x.CompareAtPrice is not null).WithMessage("The old price must be above the price.");
        RuleFor(x => x.LowStockAt).InclusiveBetween(0, 1000);
        RuleFor(x => x.Status).IsEnumName(typeof(ProductStatus), caseSensitive: false).WithMessage("Status is Draft, Published or Unpublished.");
        RuleFor(x => x.Specs).NotNull().Must(s => s.Count <= 20).WithMessage("Up to 20 details.");
        RuleForEach(x => x.Specs).ChildRules(s =>
        {
            s.RuleFor(x => x.Label).NotEmpty().MaximumLength(60);
            s.RuleFor(x => x.Value).NotEmpty().MaximumLength(200);
        });
        RuleFor(x => x.Variants).NotNull().Must(v => v.Count is >= 1 and <= 60).WithMessage("Add 1 to 60 sizes / colours.")
            .Must(v => v.Select(x => ((x.Size ?? "").Trim().ToUpperInvariant(), (x.ColorName ?? "").Trim().ToUpperInvariant())).Distinct().Count() == v.Count).WithMessage("Each size and colour pair once.");
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(x => x.Size).NotEmpty().MaximumLength(20);
            v.RuleFor(x => x.ColorName).NotEmpty().MaximumLength(40);
            v.RuleFor(x => x.ColorHex).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.ColorHex is not null).WithMessage("Colour like #1A2B3C.");
            v.RuleFor(x => x.Sku).MaximumLength(40).Matches("^[A-Za-z0-9-]*$").When(x => x.Sku is not null).WithMessage("SKU: letters, numbers and dashes.");
            v.RuleFor(x => x.Stock).InclusiveBetween(0, 100_000);
            v.RuleFor(x => x.PriceOverride).GreaterThan(0).When(x => x.PriceOverride is not null);
        });
    }
}
