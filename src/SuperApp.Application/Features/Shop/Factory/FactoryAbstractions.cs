using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Application.Features.Shop.Factory;

/// <summary>Device tokens (random, stored hashed) and staff sessions (protected with the server's keys).</summary>
public interface IFactoryTokens
{
    /// <summary>A new random device token and its hash for the database.</summary>
    (string Token, byte[] Hash) NewDeviceToken();

    byte[] Hash(string token);

    string CreateSession(long staffId, long deviceId, DateTime expiresAt);

    /// <summary>The staff and device in a session token, or null when changed, foreign or expired.</summary>
    (long StaffId, long DeviceId)? ReadSession(string token);
}

/// <summary>Factories, their staff, devices, jobs and catalogue change requests.</summary>
public interface IFactoryRepository
{
    Task<FactoryScope?> OwnerScopeAsync(Guid ownerUserId, CancellationToken ct);

    Task<FactoryScope?> StaffScopeAsync(long staffId, long factoryId, CancellationToken ct);

    /// <summary>A registered, not revoked device by its token (last seen is updated).</summary>
    Task<FactoryDevice?> DeviceAsync(string token, CancellationToken ct);

    void AddDevice(FactoryDevice device);

    Task<Domain.Entities.Factories.Factory?> FactoryAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<StaffNameDto>> StaffNamesAsync(long factoryId, CancellationToken ct);

    Task<FactoryStaffMember?> StaffAsync(long factoryId, long staffId, CancellationToken ct);

    Task<IReadOnlyList<StaffDto>> TeamAsync(long factoryId, DateTime now, CancellationToken ct);

    Task<bool> StaffNameTakenAsync(long factoryId, string name, long? exceptId, CancellationToken ct);

    void AddStaff(FactoryStaffMember staff);

    Task<FactoryOverviewDto> OverviewAsync(long factoryId, DateTime now, CancellationToken ct);

    Task<PagedResult<FactoryJobCardDto>> JobsAsync(long factoryId, FactoryJobsQuery query, long? staffId, DateTime now, CancellationToken ct);

    Task<FactoryJobDto?> JobDtoAsync(long factoryId, string code, DateTime now, CancellationToken ct);

    /// <summary>The factory's job with its item and order, tracked for changes.</summary>
    Task<Job?> JobAsync(long factoryId, string code, CancellationToken ct);

    /// <summary>Every print job of the order (to know when all are packed).</summary>
    Task<IReadOnlyList<Job>> OrderJobsAsync(long orderId, CancellationToken ct);

    Task<bool> HasFactoryShipmentAsync(long orderId, CancellationToken ct);

    Task<string> CustomerNameAsync(Guid customerId, CancellationToken ct);

    /// <summary>The art file of a job's design for one side (0 front, 1 back).</summary>
    Task<long?> ArtFileAsync(long factoryId, string code, int side, CancellationToken ct);

    void AddProblem(JobProblem problem);

    Task<IReadOnlyList<FactoryShipmentDto>> ShipmentsAsync(long factoryId, string? status, CancellationToken ct);

    Task<FactoryShipmentDto?> ShipmentAsync(long factoryId, long shipmentId, CancellationToken ct);

    /// <summary>What the courier label shows (only the factory's own parcels).</summary>
    Task<ShipmentLabel?> LabelAsync(long factoryId, long shipmentId, CancellationToken ct);

    Task<FactoryMoneyDto> MoneyAsync(long factoryId, CancellationToken ct);

    Task<FactoryReportDto> ReportAsync(long factoryId, DateOnly from, DateOnly to, CancellationToken ct);

    Task<CatalogProduct?> CatalogProductAsync(int id, CancellationToken ct);

    Task<bool> PendingChangeAsync(long factoryId, int catalogProductId, Domain.Enums.CatalogChangeKind kind, CancellationToken ct);

    void AddChange(CatalogChangeRequest change);

    Task<IReadOnlyList<CatalogueChangeDto>> ChangesAsync(long? factoryId, bool pendingOnly, CancellationToken ct);
}

/// <summary>Admin production view and catalogue change approvals.</summary>
public interface IFactoryAdminRepository
{
    Task<IReadOnlyList<AdminFactoryDto>> FactoriesAsync(DateTime now, CancellationToken ct);

    Task<PagedResult<FactoryJobCardDto>> JobsAsync(AdminJobsQuery query, DateTime now, CancellationToken ct);

    Task<Job?> JobAsync(string code, CancellationToken ct);

    Task<JobProblem?> ProblemAsync(long id, CancellationToken ct);

    Task<CatalogChangeRequest?> ChangeAsync(long id, CancellationToken ct);

    /// <summary>The printable product with colours, sizes and details, tracked for changes.</summary>
    Task<CatalogProduct?> CatalogForEditAsync(int id, CancellationToken ct);

    Task<FactoryPrice?> PriceAsync(long factoryId, int catalogProductId, CancellationToken ct);

    void AddPrice(FactoryPrice price);

    /// <summary>Sellers with designs on the product (to tell them about an approved change).</summary>
    Task<IReadOnlyList<Guid>> SellersUsingAsync(int catalogProductId, CancellationToken ct);

    Task<Order> OrderOfJobAsync(Job job, CancellationToken ct);
}

/// <summary>A courier label: tracking, receiver, cash to collect and the sender.</summary>
public sealed record ShipmentLabel(string TrackingNumber, string Provider, string OrderCode, string ReceiverName, string Phone, string AddressJson, decimal CodAmount, int Pieces,
    string SenderName, string SenderAddress, DateTime CreatedAt);

/// <summary>Factory PDFs: the job ticket with its QR, and the courier label with the Super App logo.</summary>
public interface IFactoryDocuments
{
    byte[] JobTicket(FactoryJobDto job, string factoryName);

    byte[] ShippingLabel(ShipmentLabel label);
}