using System.Globalization;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>Print-on-demand price and limit rules (FR-POD-04, FR-POD-05, FR-POD-08).</summary>
public static class DesignRules
{
    /// <summary>Base for a design: catalogue base + back print (when the back is printed) − the level discount.</summary>
    public static decimal Base(CatalogProduct c, bool backPrinted, decimal levelDiscount) => c.BasePrice + (backPrinted ? c.BackPrintExtra : 0) - levelDiscount;

    /// <summary>The highest price: the catalogue maximum, raised by the back print so the room stays the same.</summary>
    public static decimal Max(CatalogProduct c, bool backPrinted) => c.MaxPrice + (backPrinted ? c.BackPrintExtra : 0);

    /// <summary>The price may equal the base (no profit) and may not exceed the maximum.</summary>
    public static void EnsurePrice(decimal price, decimal min, decimal max)
    {
        if (price < min)
        {
            throw new BusinessRuleException($"The price can’t be below the base of {min:0} EGP.", "price_below_base");
        }
        if (price > max)
        {
            throw new BusinessRuleException($"The price can’t be above {max:0} EGP.", "price_above_max");
        }
    }

    /// <summary>The level's active-design limit (null = unlimited).</summary>
    public static void EnsureRoom(SellerLevel level, int active, int adding = 1)
    {
        if (level.ActiveDesigns is { } limit && active + adding > limit)
        {
            throw new BusinessRuleException($"Your level allows {limit} active designs. Pause or delete one, or level up.", "design_limit");
        }
    }

    /// <summary>The edit token the studio sends back: the last saved time of the design (views don't change it).</summary>
    public static string Token(Design d) => (d.UpdatedAt ?? d.CreatedAt).Ticks.ToString(CultureInfo.InvariantCulture);

    /// <summary>Print quality at the full print width: vector, good (≥ 200 dpi), ok (≥ 150) or low.</summary>
    public static (int? Dpi, string Quality) Quality(int width, bool vector, decimal printWidthCm)
    {
        if (vector)
        {
            return (null, "vector");
        }
        var dpi = (int)Math.Round(width / ((double)printWidthCm / 2.54));
        return (dpi, dpi >= 200 ? "good" : dpi >= 150 ? "ok" : "low");
    }

    /// <summary>Where new art goes: centred in the print area, a little high, 90% of its width.</summary>
    public static (decimal X, decimal Y, decimal W) DefaultPlacement(CatalogPrintArea area) =>
        (Math.Round(area.X + (area.Width / 2), 4), Math.Round(area.Y + (area.Height * 0.3m), 4), Math.Round(area.Width * 0.9m, 4));

    /// <summary>A live design that changes goes back to review; Elite stays live with a later spot check.</summary>
    public static void AfterEdit(Design d, SellerLevel level)
    {
        if (d.Status == DesignStatus.Live && level.ReviewLane == ReviewLane.InstantWithSpotCheck)
        {
            d.SpotCheck = true;
        }
        else if (d.Status == DesignStatus.Live)
        {
            d.Status = DesignStatus.InReview;
            d.SubmittedAt = DateTime.UtcNow;
        }
        else if (d.Status == DesignStatus.Rejected)
        {
            d.Status = DesignStatus.Draft;
        }
    }
}
