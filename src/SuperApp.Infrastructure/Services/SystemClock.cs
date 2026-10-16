using SuperApp.Application.Abstractions.Services;

namespace SuperApp.Infrastructure.Services;

/// <summary>The real clock (UTC).</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
