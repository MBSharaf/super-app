using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using QRCoder;
using SuperApp.Application.Features.Bookings;

namespace SuperApp.Infrastructure.Services;

/// <summary>
/// QR tokens are the booking or package code protected with the server's data-protection keys: a token
/// cannot be made or edited without the keys, so a changed QR is rejected at the gate ("bad_signature").
/// </summary>
public sealed class QrTokens(IDataProtectionProvider protection) : IQrTokens
{
    private readonly IDataProtector protector = protection.CreateProtector("SuperApp.Qr.v1");

    public string Create(string code) => WebEncoders.Base64UrlEncode(protector.Protect(System.Text.Encoding.UTF8.GetBytes(code)));

    public string? Read(string token)
    {
        try
        {
            return System.Text.Encoding.UTF8.GetString(protector.Unprotect(WebEncoders.Base64UrlDecode(token)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public byte[] Png(string token)
    {
        using var data = new QRCodeGenerator().CreateQrCode(token, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
