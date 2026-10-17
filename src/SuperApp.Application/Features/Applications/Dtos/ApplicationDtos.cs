using SuperApp.Application.Abstractions.Storage;

namespace SuperApp.Application.Features.Applications.Dtos;

/// <summary>Answers of a pitch owner application (API §2.3). <see cref="Formats"/> are 5, 7 or 11.</summary>
public sealed record PitchOwnerAnswers(
    string OwnerName,
    string Phone,
    string NationalId,
    string PitchName,
    int AreaId,
    string Address,
    decimal Lat,
    decimal Lng,
    int Count,
    List<int> Formats,
    decimal Price,
    string PayoutMethod,
    string PayoutDetail,
    bool AcceptTerms,
    string? Note = null);

/// <summary>Answers of a seller application. <see cref="Mode"/>: pod, store or both.</summary>
public sealed record SellerAnswers(
    string FullName,
    string Phone,
    string NationalId,
    string StoreName,
    string? Handle,
    string Mode,
    string? Bio,
    string PayoutMethod,
    string PayoutDetail,
    bool AcceptTerms);

/// <summary>Answers of a factory application. <see cref="Techniques"/>: DTF, DTG, Screen, Embroidery, Sublimation.</summary>
public sealed record FactoryAnswers(
    string OwnerName,
    string Phone,
    string NationalId,
    string FactoryName,
    int AreaId,
    string Address,
    int CapacityPerDay,
    List<string> Techniques,
    string PayoutDetail,
    bool AcceptTerms);

/// <summary>Files of an application: the ID card (both sides), venue photos (pitch owners), papers (factories).</summary>
public sealed record ApplicationFiles(UploadedFile? IdFront, UploadedFile? IdBack, IReadOnlyList<UploadedFile> Photos, IReadOnlyList<UploadedFile> Documents);

/// <summary>An application as the applicant and the team see it. The national ID is never returned in full.</summary>
public sealed record ApplicationResponse(
    string Code,
    string Type,
    string Status,
    object Answers,
    string? RejectReason,
    IReadOnlyList<ApplicationFileResponse> Files,
    ApplicantResponse Applicant,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

public sealed record ApplicationFileResponse(long FileId, string Purpose, string Url);

public sealed record ApplicantResponse(Guid Id, string FullName, string Email, string? Phone);

/// <summary>POST /admin/approvals/applications/{code}/reject.</summary>
public sealed record RejectRequest(string Reason);

/// <summary>GET /admin/approvals filters.</summary>
public sealed record ApprovalsQuery(string? Type, string? Status, int Page = 1, int PageSize = 20);
