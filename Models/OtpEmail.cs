namespace OutfitStudio.Models;

public sealed class EmailSettings
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = "";
    public string Password { get; set; } = "";
    public int ExpireMinutes { get; set; } = 5;
}

public sealed class OtpCodeEntry
{
    public string CodeHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
}
