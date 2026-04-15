using System;
using System.Security.Cryptography;
using System.Text;
using QRCoder;
using System.Drawing;
using Microsoft.Extensions.Configuration;

namespace Batanai.Api.Services;

public class MfaService
{
    private const int BackupCodeCount = 10;
    private const int BackupCodeLength = 8;
    private readonly IConfiguration _configuration;

    public MfaService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a random TOTP secret (Base32 encoded)
    /// </summary>
    public string GenerateMfaSecret()
    {
        var random = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(random);
        }
        return Base32Encode(random);
    }

    /// <summary>
    /// Generates backup codes for MFA recovery
    /// </summary>
    public List<string> GenerateBackupCodes()
    {
        var codes = new List<string>();
        using (var rng = RandomNumberGenerator.Create())
        {
            for (int i = 0; i < BackupCodeCount; i++)
            {
                var randomBytes = new byte[BackupCodeLength / 2];
                rng.GetBytes(randomBytes);
                codes.Add(BitConverter.ToString(randomBytes).Replace("-", "").ToUpper());
            }
        }
        return codes;
    }

    /// <summary>
    /// Verifies a TOTP code against the secret
    /// </summary>
    public bool VerifyTotpCode(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var secretBytes = Base32Decode(secret);
            var timeSteps = (long)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);

            // Check current, previous, and next time step for tolerance
            for (int i = -1; i <= 1; i++)
            {
                var totp = GenerateTotp(secretBytes, timeSteps + i);
                if (totp == code)
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private string GenerateTotp(byte[] secret, long timeCounter)
    {
        var hmacInput = new byte[8];
        for (int i = 7; i >= 0; i--)
        {
            hmacInput[i] = (byte)(timeCounter & 0xff);
            timeCounter >>= 8;
        }

        using (var hmac = new System.Security.Cryptography.HMACSHA1(secret))
        {
            var hmacOutput = hmac.ComputeHash(hmacInput);
            var offset = hmacOutput[hmacOutput.Length - 1] & 0x0f;
            var otp = (uint)(
                ((hmacOutput[offset] & 0x7f) << 24) |
                ((hmacOutput[offset + 1] & 0xff) << 16) |
                ((hmacOutput[offset + 2] & 0xff) << 8) |
                (hmacOutput[offset + 3] & 0xff)
            );
            otp = otp % 1000000;
            return otp.ToString("D6");
        }
    }

    /// <summary>
    /// Generates a QR code as a base64-encoded data URI for authenticator apps
    /// </summary>
    public string GenerateQrCodeUrl(string email, string secret, string? issuer = null)
    {
        issuer = issuer ?? _configuration["AppSettings:AppName"] ?? "Batanai - Elroitec";
        var label = $"{issuer} ({email})";
        var otpauthUrl = $"otpauth://totp/{Uri.EscapeDataString(label)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
        
        // Generate QR code using QRCoder
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpauthUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);
        
        // Convert to base64 data URI
        var base64Image = Convert.ToBase64String(qrCodeImage);
        return $"data:image/png;base64,{base64Image}";
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        int bitIndex = 0;
        int value = 0;

        foreach (var b in data)
        {
            value = (value << 8) | (int)b;
            bitIndex += 8;
            while (bitIndex >= 5)
            {
                bitIndex -= 5;
                result.Append(alphabet[(value >> bitIndex) & 31]);
            }
        }

        if (bitIndex > 0)
        {
            result.Append(alphabet[(value << (5 - bitIndex)) & 31]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var result = new List<byte>();

        foreach (var c in input.ToUpper())
        {
            var index = alphabet.IndexOf(c);
            if (index < 0)
                throw new ArgumentException($"Invalid character in base32 string: {c}");

            value = (value << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                result.Add((byte)((value >> bits) & 255));
            }
        }

        return result.ToArray();
    }
}
