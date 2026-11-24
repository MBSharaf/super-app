using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Push;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Phones registered for push (unique token).</summary>
public sealed class PushDeviceRepository(AppDbContext db) : IPushDeviceRepository
{
    public Task<PushDevice?> ByTokenAsync(string token, CancellationToken ct) => db.Set<PushDevice>().FirstOrDefaultAsync(d => d.Token == token, ct);

    public void Add(PushDevice device) => db.Set<PushDevice>().Add(device);

    public void Remove(PushDevice device) => db.Set<PushDevice>().Remove(device);
}
