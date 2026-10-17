using FluentValidation;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Validation;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>The partner's venues (API §3.2): the places that hold the pitches.</summary>
public interface IPartnerVenuesService
{
    Task<IReadOnlyList<VenueDto>> ListAsync(CancellationToken ct);

    Task<VenueDto> CreateAsync(VenueRequest request, CancellationToken ct);

    Task<VenueDto> UpdateAsync(long id, VenueRequest request, CancellationToken ct);
}

public sealed class PartnerVenuesService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    IValidator<VenueRequest> rules) : IPartnerVenuesService
{
    public async Task<IReadOnlyList<VenueDto>> ListAsync(CancellationToken ct) =>
        await partners.VenuesAsync((await access.ViewAsync(ct)).PartnerId, ct);

    public async Task<VenueDto> CreateAsync(VenueRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        await EnsureAreaAsync(request.AreaId, ct);
        var venue = new Venue { PartnerId = scope.PartnerId };
        Apply(venue, request);
        partners.AddVenue(venue);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "venue.created", $"New venue {venue.Name}", "Venue", venue.Id.ToString(), ct: ct);
        return await FindAsync(scope.PartnerId, venue.Id, ct);
    }

    public async Task<VenueDto> UpdateAsync(long id, VenueRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var venue = await partners.VenueAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Venue not found.");
        await EnsureAreaAsync(request.AreaId, ct);
        Apply(venue, request);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "venue.updated", $"Venue {venue.Name} edited", "Venue", venue.Id.ToString(), ct: ct);
        return await FindAsync(scope.PartnerId, venue.Id, ct);
    }

    private static void Apply(Venue venue, VenueRequest r)
    {
        venue.Name = r.Name.Trim();
        venue.AreaId = r.AreaId;
        venue.Address = r.Address.Trim();
        venue.Lat = r.Lat;
        venue.Lng = r.Lng;
        venue.Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
    }

    private async Task EnsureAreaAsync(int areaId, CancellationToken ct)
    {
        if (!await partners.AreaExistsAsync(areaId, ct))
        {
            throw new BadRequestException("Choose an area from the list.", "unknown_area");
        }
    }

    private async Task<VenueDto> FindAsync(long partnerId, long venueId, CancellationToken ct) =>
        (await partners.VenuesAsync(partnerId, ct)).Single(v => v.Id == venueId);
}

public sealed class VenueRequestValidator : AbstractValidator<VenueRequest>
{
    public VenueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 120);
        RuleFor(x => x.AreaId).GreaterThan(0).WithMessage("Choose an area.");
        RuleFor(x => x.Address).NotEmpty().Length(5, 300);
        RuleFor(x => x.Lat).InclusiveBetween(22, 32).WithMessage("Pin the venue on the map (inside Egypt).");
        RuleFor(x => x.Lng).InclusiveBetween(24, 37).WithMessage("Pin the venue on the map (inside Egypt).");
        RuleFor(x => x.Phone!).EgyptianMobile().When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
