
namespace OutfitStudio.Models;

public class User
{
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string Role { get; set; } = "User";
    public bool IsLocked
    {
        get; set;
    }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDeleted => IsLocked && PasswordHash.Length == 0;

    [System.Text.Json.Serialization.JsonIgnore]
    public string PasswordVersion => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(PasswordHash)));
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

