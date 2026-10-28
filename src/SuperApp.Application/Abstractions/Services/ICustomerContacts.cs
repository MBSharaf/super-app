namespace SuperApp.Application.Abstractions.Services;

/// <summary>A customer's name, email and mobile — sent with order and booking events to the owner's automation (n8n).</summary>
public interface ICustomerContacts
{
    Task<CustomerContact> GetAsync(Guid userId, CancellationToken ct);
}

public sealed record CustomerContact(string Name, string? Email, string? Phone);
