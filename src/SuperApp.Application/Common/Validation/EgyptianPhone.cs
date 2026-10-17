using PhoneNumbers;

namespace SuperApp.Application.Common.Validation;

/// <summary>Egyptian mobile numbers (010, 011, 012, 015), checked and stored in one format: +201XXXXXXXXX.</summary>
public static class EgyptianPhone
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    /// <summary>The number in +20 format, or null when it is not a valid Egyptian mobile number.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 20)
        {
            return null;
        }
        try
        {
            var number = Util.Parse(input, "EG");
            var isMobile = Util.GetNumberType(number) is PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE;
            return Util.IsValidNumberForRegion(number, "EG") && isMobile ? Util.Format(number, PhoneNumberFormat.E164) : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    public static bool IsValid(string? input) => Normalize(input) is not null;
}
