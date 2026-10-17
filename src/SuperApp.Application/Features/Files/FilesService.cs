using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Application.Features.Files;

/// <summary>
/// Serves stored files. Public files (pitch and product photos) to anyone; private files (ID cards, company
/// papers) only to their owner and to staff who check IDs. Anyone else gets 404, so private files cannot be guessed.
/// </summary>
public interface IFilesService
{
    Task<FileDownload> OpenAsync(long fileId, CancellationToken ct);
}

public sealed class FilesService(IFileStorage storage, ICurrentUser currentUser) : IFilesService
{
    private static readonly string[] CheckerPermissions = ["kyc.read", "approvals.partners", "approvals.sellers", "approvals.factories"];

    public async Task<FileDownload> OpenAsync(long fileId, CancellationToken ct)
    {
        var file = await storage.OpenAsync(fileId, ct) ?? throw new NotFoundException("This file does not exist.");
        if (file.IsPrivate && file.OwnerUserId != currentUser.UserId && !CheckerPermissions.Any(currentUser.HasPermission))
        {
            await file.Content.DisposeAsync();
            throw new NotFoundException("This file does not exist.");
        }
        return file;
    }
}
