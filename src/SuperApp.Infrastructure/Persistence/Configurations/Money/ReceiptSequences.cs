using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>One SQL sequence per receipt prefix (RC, RF, PA, TU, ST): unique receipt numbers under concurrency.</summary>
public static class ReceiptSequences
{
    public static readonly string[] Prefixes = ["RC", "RF", "PA", "TU", "ST"];

    public static string Prefix(ReceiptKind kind) => kind switch
    {
        ReceiptKind.Refund => "RF",
        ReceiptKind.PayoutAdvice => "PA",
        ReceiptKind.TopUp => "TU",
        ReceiptKind.Statement => "ST",
        _ => "RC",
    };

    public static string Name(string prefix) => $"ReceiptNumbers_{prefix}";

    /// <summary>Fixed SQL per prefix (no user input reaches it).</summary>
    public static string NextValueSql(string prefix) => prefix switch
    {
        "RF" => "SELECT NEXT VALUE FOR [money].[ReceiptNumbers_RF] AS [Value]",
        "PA" => "SELECT NEXT VALUE FOR [money].[ReceiptNumbers_PA] AS [Value]",
        "TU" => "SELECT NEXT VALUE FOR [money].[ReceiptNumbers_TU] AS [Value]",
        "ST" => "SELECT NEXT VALUE FOR [money].[ReceiptNumbers_ST] AS [Value]",
        _ => "SELECT NEXT VALUE FOR [money].[ReceiptNumbers_RC] AS [Value]",
    };

    public static void Apply(ModelBuilder builder)
    {
        foreach (var prefix in Prefixes)
        {
            builder.HasSequence<long>(Name(prefix), "money").StartsAt(1).IncrementsBy(1);
        }
    }
}
