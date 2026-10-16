namespace SuperApp.Domain.Enums;

public enum WalletType : byte
{
    Customer = 0,
    PartnerBookings = 1,
    PartnerStore = 2,
    Seller = 3,
    Factory = 4,
    PlatformRevenue = 5,
    PlatformClearing = 6,
}
