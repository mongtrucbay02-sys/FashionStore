
namespace OutfitStudio.Models;

public class User
{
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public string Role { get; set; } = "User";
    public bool IsLocked
    {
        get; set;
    }
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

