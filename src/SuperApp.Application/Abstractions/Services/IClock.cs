namespace SuperApp.Application.Abstractions.Services;

/// <summary>The current time in UTC (replaceable in tests).</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
