using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Applications.Services;
using SuperApp.Application.Features.Auth.Services;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Ceo.Services;
using SuperApp.Application.Features.CheckIn;
using SuperApp.Application.Features.Files;
using SuperApp.Application.Features.Packages;
using SuperApp.Application.Features.Partners;
using SuperApp.Application.Features.Reviews;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Application;

/// <summary>Registers the Application layer: business services and the request validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Stop at the first failed rule of a field: no "too short" after "missing", and no rule runs on an empty value
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMeService, MeService>();
        services.AddScoped<IAdminAccountsService, AdminAccountsService>();
        services.AddScoped<IApplicationsService, ApplicationsService>();
        services.AddScoped<IApprovalsService, ApprovalsService>();
        services.AddScoped<IFilesService, FilesService>();
        services.AddScoped<IPitchesService, PitchesService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<BookingPlanner>();
        services.AddScoped<IBookingsService, BookingsService>();
        services.AddScoped<IMyBookingsService, MyBookingsService>();
        services.AddScoped<BookingNotices>();
        services.AddScoped<Features.Payments.IPaymentListener, BookingPaymentListener>();
        services.AddScoped<Features.Payments.IPaymentsService, Features.Payments.PaymentsService>();
        services.AddScoped<IPartnerAccess, PartnerAccess>();
        services.AddScoped<IPartnerVenuesService, PartnerVenuesService>();
        services.AddScoped<IPartnerPitchesService, PartnerPitchesService>();
        services.AddScoped<IPartnerClosuresService, PartnerClosuresService>();
        services.AddScoped<IPartnerBookingsService, PartnerBookingsService>();
        services.AddScoped<IPartnerPhotosService, PartnerPhotosService>();
        services.AddScoped<IPartnerPitchRemovalService, PartnerPitchRemovalService>();
        services.AddScoped<IPitchReviewsService, PitchReviewsService>();
        services.AddScoped<IPartnerStaffService, PartnerStaffService>();
        services.AddScoped<ICheckInService, CheckInService>();
        services.AddScoped<IPackageOptionsService, PackageOptionsService>();
        services.AddScoped<IPackagesService, PackagesService>();
        Features.Shop.ShopRegistration.AddShop(services);
        services.AddScoped<IReviewsService, ReviewsService>();
        services.AddScoped<Features.Waitlist.IWaitlistService, Features.Waitlist.WaitlistService>();
        services.AddScoped<IPartnerReviewsService, PartnerReviewsService>();
        services.AddScoped<Features.FreeSlots.IFreeSlotsService, Features.FreeSlots.FreeSlotsService>();
        services.AddScoped<Features.Games.IGamesService, Features.Games.GamesService>();
        services.AddScoped<Features.Webhooks.IWebhooksService, Features.Webhooks.WebhooksService>();
        Features.Fantasy.FantasyRegistration.AddFantasy(services);   // Phase 6 (branch phase-6-fantasy)
        Features.AdminConsole.AdminRegistration.AddAdmin(services);   // Phase 7 (branch phase-7-admin)
        return services;
    }
}
