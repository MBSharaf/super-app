using SuperApp.Application.Features.Shop.Catalog;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>Who is working: the owner (signed in with the account) or a staff member signed in with a PIN on a registered device.</summary>
public sealed record FactoryMeDto(long FactoryId, string FactoryName, long? StaffId, string Name, string Role, IReadOnlyList<string> Can, bool IntakePaused, string Status);

public sealed record RegisterDeviceRequest(string Name);

/// <summary>The device token: keep it on the device (header X-Factory-Device). It is shown once.</summary>
public sealed record DeviceTokenDto(long DeviceId, string Name, string Token);

public sealed record StaffNameDto(long Id, string Name, string Role);

public sealed record PinRequest(long StaffId, string Pin);

/// <summary>The staff session (header X-Factory-Session), valid for 12 hours on this device.</summary>
public sealed record FactorySessionDto(string Token, DateTime ExpiresAt, FactoryMeDto Me);

public sealed record FactoryOverviewDto(int New, int Printing, int QualityCheck, int Packing, int Ready, int Late, int DueToday, int CapacityPerDay, int DoneToday, int OpenProblems, int InTransit);

/// <summary>Production board filter: stage = new | printing | qualitycheck | packing | ready | shipped | cancelled | active (default); mine = assigned to me; late.</summary>
public sealed record FactoryJobsQuery(string? Stage = null, bool Mine = false, bool Late = false, string? Q = null, int Page = 1, int PageSize = 50);

public sealed record FactoryJobCardDto(string Code, string Stage, DateTime DueAt, bool Late, string OrderCode, string Title, string ProductName, string Size, string Color, int Qty,
    bool HasFront, bool HasBack, string? AssignedTo, int ReprintCount, bool IsSample, string City);

public sealed record JobEventDto(string From, string To, string? StaffName, string? Note, DateTime At);

public sealed record JobProblemDto(long Id, string Kind, string Detail, string Status, string? Resolution, DateTime CreatedAt);

/// <summary>The job page with print files (served by the factory endpoint) and placement on the garment.</summary>
public sealed record FactoryJobDto(FactoryJobCardDto Card, string ProductKey, string? FrontArtUrl, string? BackArtUrl, PlacementDto? Front, PlacementDto? Back,
    IReadOnlyList<PrintAreaDto> PrintAreas, IReadOnlyList<JobEventDto> Events, IReadOnlyList<JobProblemDto> Problems, decimal DeductionAmount);

public sealed record AdvanceRequest(string ToStage, string? Note);

public sealed record QcRequest(bool Pass, string? Reason);

/// <summary>Kind: OutOfBlanks, MachineDown, BadArtwork, Damaged, Other.</summary>
public sealed record ProblemRequest(string Kind, string Detail);

public sealed record AssignRequest(long? StaffId);

public sealed record ScanRequest(string Code);

public sealed record FactoryShipmentDto(long Id, string OrderCode, string Provider, string TrackingNumber, string Status, decimal CodAmount, int Pieces, DateTime CreatedAt, string City);

/// <summary>Team member. Role: Supervisor, Printer, QualityCheck, Packer (the owner signs in with the account).</summary>
public sealed record StaffDto(long Id, string Name, string Role, bool IsActive, bool Locked, DateTime CreatedAt);

public sealed record StaffRequest(string Name, string Role, string? Pin, bool IsActive = true);

public sealed record ResetPinRequest(string Pin);

public sealed record FactoryMoneyLineDto(string JobCode, string OrderCode, DateTime Date, string Title, int Qty, decimal Earning, decimal Deduction, bool IsSample);

/// <summary>Factory earnings per piece, deductions (reprints after complaints), and the statement. Payouts arrive with Phase 5.</summary>
public sealed record FactoryMoneyDto(decimal Earned, decimal Deductions, decimal Net, int Pieces, IReadOnlyList<FactoryMoneyLineDto> Lines);

public sealed record StaffReportDto(string Name, string Role, int Steps, int Printed, int QcPassed, int QcFailed, int Packed);

public sealed record FactoryReportDto(DateOnly From, DateOnly To, int JobsDone, int OnTime, decimal OnTimePercent, int QcFails, decimal DefectPercent, IReadOnlyList<StaffReportDto> Staff);

public sealed record FactorySettingsDto(string Name, string Address, int CapacityPerDay, int SlaHours, string Techniques, bool IntakePaused, string Status);

public sealed record FactorySettingsRequest(int CapacityPerDay, int SlaHours, bool IntakePaused);

/// <summary>
/// A catalogue change the factory asks for (FR-POD-12). Kind and <c>Change</c> (JSON):
/// Price {"pricePerPiece": 230} · Colour {"colorId": 3, "active": false} or {"add": {"nameEn","nameAr","hex"}} ·
/// Size {"add": {"size","chestCm","lengthCm","sleeveCm","isBigSize"}} · PauseProduct {"paused": true} · Details {"details": [{"labelEn","labelAr","valueEn","valueAr"}]}.
/// </summary>
public sealed record CatalogueChangeRequest(int CatalogProductId, string Kind, System.Text.Json.JsonElement Change, string Reason);

public sealed record CatalogueChangeDto(long Id, int CatalogProductId, string ProductName, string FactoryName, string Kind, string ChangeJson, string Reason, string Status,
    string? ReviewNote, DateTime CreatedAt, DateTime? ReviewedAt);

/// <summary>Admin: the production view filter.</summary>
public sealed record AdminJobsQuery(bool Late = false, bool Problem = false, long? FactoryId = null, int Page = 1, int PageSize = 50);

public sealed record ReassignRequest(long FactoryId);

public sealed record ReprintRequest(string Reason, decimal Deduction);

public sealed record ResolveRequest(string Resolution);

public sealed record AdminFactoryDto(long Id, string Name, string OwnerName, int CapacityPerDay, int SlaHours, bool IntakePaused, string Status, int OpenJobs, int LateJobs, int OpenProblems);
