using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using OutfitStudio.Models;
namespace OutfitStudio.Services;

public static class AccountIdentity
{
    public static ClaimsPrincipal Create(User user) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.FullName),
        new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role)
    }, CookieAuthenticationDefaults.AuthenticationScheme));
}
