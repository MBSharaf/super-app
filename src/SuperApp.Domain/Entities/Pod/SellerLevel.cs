using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>Seller level (Starter, Rising, Pro, Elite) and its perks. Table <c>pod.SellerLevels</c>.</summary>
public class SellerLevel : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public byte Rank { get; set; }
    public int SalesNeeded { get; set; }
    public decimal MinRating { get; set; }
    public int? ActiveDesigns { get; set; }
    public int? ActiveCodes { get; set; }
    public decimal BaseDiscount { get; set; }
    public decimal StoreCommission { get; set; }
    public ReviewLane ReviewLane { get; set; }
    public byte SearchBoost { get; set; }
    public decimal SampleDiscount { get; set; }
    public byte FreeSamplesPerMonth { get; set; }
    public decimal FantasyDiscount { get; set; }
    public bool BulkUpload { get; set; }
    public bool TeamKits { get; set; }
    public bool EarlyAccess { get; set; }
    public AnalyticsTier AnalyticsTier { get; set; }
    public byte CalendarTier { get; set; }
    public SupportTier SupportTier { get; set; }
}
