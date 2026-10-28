using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Services;

public sealed class CustomerContacts(AppDbContext db) : ICustomerContacts
{
    public async Task<CustomerContact> GetAsync(Guid userId, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new CustomerContact(u.FullName, u.Email, u.PhoneNumber)).FirstOrDefaultAsync(ct)
            ?? new CustomerContact("", null, null);
}
