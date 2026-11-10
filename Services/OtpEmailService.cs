using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OutfitStudio.Models;

namespace OutfitStudio.Services;

public sealed class OtpCooldownException : Exception
{
    public OtpCooldownException() : base("Chờ ít nhất 60 giây trước khi gửi mã mới.") { }
}

public sealed class OtpEmailService(IOptions<EmailSettings> options, IMemoryCache cache)
{
    private readonly EmailSettings settings = options.Value;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private const int MaxAttempts = 5;

    public async Task SendCodeAsync(string email, string purpose, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new InvalidOperationException("Địa chỉ email không hợp lệ.");
        if (string.IsNullOrWhiteSpace(settings.FromEmail) || string.IsNullOrWhiteSpace(settings.Password))
            throw new InvalidOperationException("Thiếu SMTP_USER hoặc SMTP_PASS trong .env.");
        if (settings.ExpireMinutes is < 1 or > 15 || settings.Port is < 1 or > 65535)
            throw new InvalidOperationException("Cấu hình SMTP_PORT hoặc thời hạn OTP không hợp lệ.");

        var key = Key(normalized, purpose);
        if (cache.TryGetValue(key + ":cooldown", out _)) throw new OtpCooldownException();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, "FashionStore"),
            Subject = "Mã xác thực FashionStore",
            Body = $"Mã OTP {purpose} của bạn là: {code}\nMã có hiệu lực trong {settings.ExpireMinutes} phút. Không chia sẻ mã này với người khác.",
            SubjectEncoding = Encoding.UTF8,
            BodyEncoding = Encoding.UTF8
        };
        message.To.Add(address);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.FromEmail, settings.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 30000
        };
        await client.SendMailAsync(message, cancellationToken);

        cache.Set(key, new OtpCodeEntry
        {
            CodeHash = Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(settings.ExpireMinutes)
        }, TimeSpan.FromMinutes(settings.ExpireMinutes));
        cache.Set(key + ":cooldown", true, ResendCooldown);
    }

    public bool VerifyCode(string email, string code, string purpose)
    {
        var key = Key(email, purpose);
        if (!cache.TryGetValue(key, out OtpCodeEntry? entry) || entry is null) return false;
        if (entry.ExpiresAt <= DateTimeOffset.UtcNow || entry.Attempts >= MaxAttempts)
        {
            cache.Remove(key);
            return false;
        }

        entry.Attempts++;
        var valid = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(entry.CodeHash), Convert.FromHexString(Hash((code ?? "").Trim())));
        if (valid || entry.Attempts >= MaxAttempts) cache.Remove(key);
        return valid;
    }

    private static string Key(string email, string purpose) => $"account:otp:{purpose}:{email.Trim().ToLowerInvariant()}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
