using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// Pitch photos (API §3.2): up to 10 per pitch, checked and re-encoded by the file storage; the first one is the
/// cover. A live pitch always keeps at least one photo. Photo changes of a live pitch stay live.
/// </summary>
public interface IPartnerPhotosService
{
    Task<IReadOnlyList<PartnerPhotoDto>> AddAsync(long pitchId, IReadOnlyList<UploadedFile> files, CancellationToken ct);

    Task<IReadOnlyList<PartnerPhotoDto>> DeleteAsync(long pitchId, long photoId, CancellationToken ct);

    Task<IReadOnlyList<PartnerPhotoDto>> OrderAsync(long pitchId, PhotoOrderRequest request, CancellationToken ct);
}

public sealed class PartnerPhotosService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IValidator<PhotoOrderRequest> orderRules) : IPartnerPhotosService
{
    public const int MaxPhotos = 10;

    public async Task<IReadOnlyList<PartnerPhotoDto>> AddAsync(long pitchId, IReadOnlyList<UploadedFile> files, CancellationToken ct)
    {
        if (files.Count == 0)
        {
            throw new BadRequestException("Choose at least one photo.", "no_photo");
        }
        var (scope, pitch) = await PitchAsync(pitchId, ct);
        if (pitch.Photos.Count + files.Count > MaxPhotos)
        {
            throw new BusinessRuleException($"A pitch can have up to {MaxPhotos} photos. Delete some first.", "too_many_photos");
        }
        var next = pitch.Photos.Count == 0 ? 1 : pitch.Photos.Max(p => p.SortOrder) + 1;
        foreach (var file in files)
        {
            var fileId = await storage.SaveAsync(file, UploadKind.Photo, scope.OwnerUserId, "photos", ct);
            pitch.Photos.Add(new PitchPhoto { FileId = fileId, SortOrder = next++ });
        }
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "pitch.photos_added", $"{files.Count} photo(s) added to {pitch.Name}", "Pitch", pitch.Slug, ct: ct);
        return await PhotosAsync(scope, pitchId, ct);
    }

    public async Task<IReadOnlyList<PartnerPhotoDto>> DeleteAsync(long pitchId, long photoId, CancellationToken ct)
    {
        var (scope, pitch) = await PitchAsync(pitchId, ct);
        var photo = pitch.Photos.FirstOrDefault(p => p.Id == photoId) ?? throw new NotFoundException("Photo not found.");
        if (pitch.Photos.Count == 1 && pitch.Status == PitchStatus.Live)
        {
            throw new BusinessRuleException("A live pitch needs at least one photo. Add another one first.", "last_photo");
        }
        pitch.Photos.Remove(photo);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "pitch.photo_removed", $"A photo removed from {pitch.Name}", "Pitch", pitch.Slug, ct: ct);
        return await PhotosAsync(scope, pitchId, ct);
    }

    public async Task<IReadOnlyList<PartnerPhotoDto>> OrderAsync(long pitchId, PhotoOrderRequest request, CancellationToken ct)
    {
        await orderRules.ValidateAndThrowAsync(request, ct);
        var (scope, pitch) = await PitchAsync(pitchId, ct);
        if (!pitch.Photos.Select(p => p.Id).ToHashSet().SetEquals(request.PhotoIds) || request.PhotoIds.Count != pitch.Photos.Count)
        {
            throw new BadRequestException("Send every photo of the pitch once.", "photo_ids");
        }
        for (var i = 0; i < request.PhotoIds.Count; i++)
        {
            pitch.Photos.Single(p => p.Id == request.PhotoIds[i]).SortOrder = i + 1;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return await PhotosAsync(scope, pitchId, ct);
    }

    private async Task<(PartnerScope Scope, Pitch Pitch)> PitchAsync(long pitchId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        return (scope, pitch);
    }

    private async Task<IReadOnlyList<PartnerPhotoDto>> PhotosAsync(PartnerScope scope, long pitchId, CancellationToken ct) =>
        (await partners.PitchForEditAsync(scope.PartnerId, pitchId, ct))!.Photos;
}

public sealed class PhotoOrderRequestValidator : AbstractValidator<PhotoOrderRequest>
{
    public PhotoOrderRequestValidator()
    {
        RuleFor(x => x.PhotoIds).NotEmpty().Must(ids => ids.Count <= PartnerPhotosService.MaxPhotos && ids.Distinct().Count() == ids.Count)
            .WithMessage("Send every photo id once.");
    }
}
