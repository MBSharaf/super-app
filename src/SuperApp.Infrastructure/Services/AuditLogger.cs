using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Infrastructure.Services;

/// <summary>Writes audit lines with the actor, IP and correlation id of the current request. The log is never edited.</summary>
public sealed class AuditLogger(AppDbContext db, ICurrentUser currentUser, IRequestInfo request, IClock clock) : IAuditLogger
{
    public async Task LogAsync(AuditSystem system, string action, string summary, string? entityType = null, string? entityId = null,
        object? data = null, CancellationToken ct = default)
    {
        db.Set<AuditLog>().Add(new AuditLog
        {
            At = clock.UtcNow,
            ActorUserId = currentUser.UserId,
            System = system,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary.Length > 400 ? summary[..400] : summary,
            DataJson = data is null ? null : SeedFile.ToJson(data),
            Ip = request.IpAddress,
            CorrelationId = request.CorrelationId,
        });
        await db.SaveChangesAsync(ct);
    }
}
