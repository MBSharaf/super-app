using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Abstractions.Persistence;

/// <summary>Partner, seller and factory applications (auth.Applications) and the ID checks that come with them.</summary>
public interface IApplicationRepository
{
    Task<string> NewCodeAsync(CancellationToken ct);

    void Add(PartnerApplication application);

    /// <summary>The application with its files, tracked for changes; null when the code does not exist.</summary>
    Task<PartnerApplication?> FindAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<PartnerApplication>> MineAsync(Guid applicantId, CancellationToken ct);

    Task<PagedResult<PartnerApplication>> ListAsync(IReadOnlyCollection<ApplicationType> types, ApplicationStatus? status, int page, int pageSize, CancellationToken ct);

    Task<bool> HasOpenAsync(Guid applicantId, ApplicationType type, CancellationToken ct);

    /// <summary>True when the person already is a pitch partner / seller / factory owner.</summary>
    Task<bool> IsAlreadyMemberAsync(Guid applicantId, ApplicationType type, CancellationToken ct);

    Task<ApplicantResponse> ApplicantAsync(Guid userId, CancellationToken ct);

    Task<bool> AreaExistsAsync(int areaId, CancellationToken ct);

    Task<bool> StoreHandleTakenAsync(string handle, CancellationToken ct);

    Task<bool> FactoryNameTakenAsync(string name, CancellationToken ct);

    /// <summary>Records the ID card for checking; the national ID number is stored encrypted.</summary>
    Task SaveKycAsync(Guid userId, string nationalId, long frontFileId, long backFileId, CancellationToken ct);

    Task DecideKycAsync(Guid userId, bool verified, Guid reviewerId, string? reason, CancellationToken ct);
}

/// <summary>Creates the business records when an application is approved.</summary>
public interface IOnboardingRepository
{
    /// <summary>Partner + one venue with the requested pitches (live, standard cancellation policy) and their photos.</summary>
    Task OnboardPitchOwnerAsync(Guid userId, PitchOwnerAnswers answers, IReadOnlyList<long> photoFileIds, CancellationToken ct);

    /// <summary>Seller store (with its link) + seller profile at the first level.</summary>
    Task OnboardSellerAsync(Guid userId, SellerAnswers answers, CancellationToken ct);

    /// <summary>Factory (active) owned by the applicant.</summary>
    Task OnboardFactoryAsync(Guid userId, FactoryAnswers answers, CancellationToken ct);
}
