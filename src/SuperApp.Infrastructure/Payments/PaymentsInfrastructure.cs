using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Payments;

public sealed class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "PY-" + new string(Enumerable.Range(0, 8).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
            if (!await db.Set<Payment>().AnyAsync(p => p.Code == code, ct) && !db.Set<Payment>().Local.Any(p => p.Code == code))
            {
                return code;
            }
        }
    }

    public void Add(Payment payment) => db.Set<Payment>().Add(payment);

    public Task<Payment?> FindAsync(string code, CancellationToken ct) => db.Set<Payment>().FirstOrDefaultAsync(p => p.Code == code, ct);
}

public sealed class PayerDirectory(AppDbContext db) : IPayerDirectory
{
    public async Task<(string Name, string Email, string Phone)> FindAsync(Guid userId, CancellationToken ct)
    {
        var u = await db.Set<User>().Where(x => x.Id == userId).Select(x => new { x.FullName, x.Email, x.PhoneNumber }).FirstAsync(ct);
        return (u.FullName, u.Email ?? string.Empty, u.PhoneNumber ?? string.Empty);
    }
}

/// <summary>
/// Every minute: bookings still waiting for their payment 15 minutes after they were made are cancelled, their hours freed
/// and the payment marked failed. Off in the "Testing" environment (tests call <see cref="RunOnceAsync"/>).
/// </summary>
public sealed class AwaitingPaymentExpiry(IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<AwaitingPaymentExpiry> logger) : BackgroundService
{
    public static readonly TimeSpan PayWithin = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Expiring unpaid bookings failed; trying again shortly.");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var late = await db.Set<Booking>().Include(b => b.Payment)
            .Where(b => b.Status == BookingStatus.AwaitingPayment && b.CreatedAt <= now - PayWithin).Take(100).ToListAsync(ct);
        foreach (var b in late)
        {
            b.Status = BookingStatus.CancelledByCustomer;
            b.CancelledAt = now;
            if (b.Payment is { Status: PaymentStatus.Pending or PaymentStatus.Created } p)
            {
                p.Status = PaymentStatus.Failed;
            }
        }
        await db.SaveChangesAsync(ct);
        var ids = late.Select(b => b.Id).ToList();
        await db.Set<BookingSlot>().Where(s => ids.Contains(s.BookingId)).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, false), ct);
        // Tournament teams whose entry fee was never paid give their spot back
        var teams = await db.Set<TournamentTeam>().Include(t => t.Payment)
            .Where(t => t.Status == TournamentTeamStatus.Registered && t.Payment != null && t.Payment.Status == PaymentStatus.Pending && t.Payment.CreatedAt <= now - PayWithin).Take(100).ToListAsync(ct);
        foreach (var t in teams)
        {
            t.Status = TournamentTeamStatus.Withdrawn;
            t.Payment!.Status = PaymentStatus.Failed;
        }
        var requests = await db.Set<GameRequest>().Include(r => r.Payment)
            .Where(r => r.Status != GameRequestStatus.Withdrawn && r.Payment != null && r.Payment.Status == PaymentStatus.Pending && r.Payment.CreatedAt <= now - PayWithin).Take(100).ToListAsync(ct);
        foreach (var r in requests)
        {
            r.Status = GameRequestStatus.Withdrawn;
            r.Payment!.Status = PaymentStatus.Failed;
        }
        await db.SaveChangesAsync(ct);
        return late.Count + teams.Count + requests.Count;
    }
}

public static class PaymentsInfrastructure
{
    /// <summary>Paymob when its secret key is in .env, else the simulated gateway.</summary>
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPayerDirectory, PayerDirectory>();
        if (!string.IsNullOrWhiteSpace(configuration["Payments:Paymob:SecretKey"]))
        {
            services.AddHttpClient<IPaymentGateway, PaymobGateway>(http =>
            {
                http.BaseAddress = new Uri(PaymobGateway.BaseAddress);
                http.Timeout = TimeSpan.FromSeconds(20);
            });
        }
        else
        {
            services.AddSingleton<IPaymentGateway, SimulatedGateway>();
        }
        services.AddScoped<SuperApp.Application.Features.Money.IMoneyStore, MoneyStore>();
        services.AddSingleton<SuperApp.Application.Features.Money.IMoneyDocuments, MoneyDocuments>();
        services.AddScoped<SuperApp.Application.Features.Money.IPayoutStore, PayoutStore>();
        services.AddScoped<SuperApp.Application.Features.Money.ILoyaltyStore, LoyaltyStore>();
        services.AddScoped<SuperApp.Application.Features.Money.IPartnerCodeStore, LoyaltyStore>();
        services.AddSingleton<MoneyReleaseJob>();
        services.AddHostedService(sp => sp.GetRequiredService<MoneyReleaseJob>());
        services.AddSingleton<AwaitingPaymentExpiry>();
        services.AddHostedService(sp => sp.GetRequiredService<AwaitingPaymentExpiry>());
        return services;
    }
}
