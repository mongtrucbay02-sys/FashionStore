using Microsoft.AspNetCore.Identity;
using OutfitStudio.Models;
namespace OutfitStudio.Services;

public static class AdminSeed
{
    public static void Run(AppStore store, string? email = null)
    {
        email = (email ?? "admin@fashionstore.local").Trim().ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException("AdminSeed:Email phải là địa chỉ email hợp lệ.");
        var added = false;
        store.Modify<User>("users.json", users =>
        {
            if (users.Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
                return;
            var user = new User { FullName = "Quản trị demo", Email = email, Role = "Admin" };
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, "AdminDemo!2026");
            users.Add(user);
            added = true;
        });
        Console.WriteLine(added ? "Đã tạo tài khoản Admin demo. Xem README để đăng nhập." : "Email Admin demo đã tồn tại; giữ nguyên tài khoản hiện có.");
    }
}

