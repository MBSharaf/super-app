namespace SuperApp.Application.Abstractions.Services;

/// <summary>Who is calling: a signed-in person, an AI agent's key, or nobody.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}
