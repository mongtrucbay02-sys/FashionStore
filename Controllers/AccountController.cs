using System.ComponentModel.DataAnnotations;
using OutfitStudio.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class AccountController(AppStore store, OtpEmailService otpEmail) : Controller
{
    private const string UsersFile = "users.json";
    private const string LoginOtpEmail = "account.login.otp.email";
    private const string LoginOtpUserId = "account.login.otp.userId";
    private const string LoginOtpReturnUrl = "account.login.otp.returnUrl";
    private readonly PasswordHasher<User> hasher = new();

    [HttpGet] public IActionResult Register() => View(new RegisterVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        if (store.Read<User>(UsersFile).Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError(nameof(vm.Email), "Email đã được đăng ký.");
            return View(vm);
        }
        if (!otpEmail.VerifyCode(email, vm.OtpCode, "register"))
        {
            ModelState.AddModelError(nameof(vm.OtpCode), "Mã OTP không đúng, hết hạn hoặc đã vượt quá số lần thử.");
            return View(vm);
        }

        var user = new User { FullName = vm.FullName.Trim(), Email = email, EmailVerifiedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = hasher.HashPassword(user, vm.Password);
        var created = store.Atomic(() =>
        {
            if (store.Read<User>(UsersFile).Any(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) return false;
            store.Modify<User>(UsersFile, users => users.Add(user));
            return true;
        });
        if (!created)
        {
            ModelState.AddModelError(nameof(vm.Email), "Email đã được đăng ký.");
            return View(vm);
        }
        await SignIn(user);
        return AfterLogin(user, null);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()))
            return Json(new { success = false, message = "Email không hợp lệ." });
        var normalized = email.Trim().ToLowerInvariant();
        if (store.Read<User>(UsersFile).Any(u => u.Email.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return Json(new { success = false, message = "Email này đã được đăng ký." });

        try
        {
            await otpEmail.SendCodeAsync(normalized, "register", HttpContext.RequestAborted);
            return Json(new { success = true, message = "Mã OTP đã được gửi qua Gmail." });
        }
        catch (OtpCooldownException)
        {
            return Json(new { success = false, message = "Hãy đợi 60 giây trước khi yêu cầu mã khác." });
        }
        catch (OperationCanceledException)
        {
            return Json(new { success = false, message = "Gửi OTP bị hủy hoặc quá thời gian chờ. Kiểm tra mạng và cấu hình SMTP." });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
        {
            return Json(new { success = false, message = "Không thể gửi OTP. Kiểm tra cấu hình Gmail trong .env." });
        }
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVM());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM vm, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(vm);
        var email = vm.Email.Trim().ToLowerInvariant();
        var user = store.Read<User>(UsersFile).FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (user is null || user.IsLocked || hasher.VerifyHashedPassword(user, user.PasswordHash, vm.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(vm);
        }

        try
        {
            await otpEmail.SendCodeAsync(email, "login", HttpContext.RequestAborted);
        }
        catch (OtpCooldownException)
        {
            ModelState.AddModelError(string.Empty, "Hãy đợi 60 giây trước khi yêu cầu mã đăng nhập khác.");
            return View(vm);
        }
        catch (OperationCanceledException)
        {
            ModelState.AddModelError(string.Empty, "Gửi OTP bị hủy hoặc quá thời gian chờ. Kiểm tra mạng và cấu hình SMTP.");
            return View(vm);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Net.Mail.SmtpException)
        {
            ModelState.AddModelError(string.Empty, "Không thể gửi OTP. Kiểm tra cấu hình Gmail trong .env.");
            return View(vm);
        }

        HttpContext.Session.SetString(LoginOtpEmail, email);
        HttpContext.Session.SetString(LoginOtpUserId, user.Id.ToString());
        HttpContext.Session.SetString(LoginOtpReturnUrl, Url.IsLocalUrl(returnUrl) ? returnUrl! : "");
        return RedirectToAction(nameof(VerifyLoginOtp));
    }

    [HttpGet]
    public IActionResult VerifyLoginOtp()
    {
        var email = HttpContext.Session.GetString(LoginOtpEmail);
        return string.IsNullOrWhiteSpace(email) ? RedirectToAction(nameof(Login)) : View(new OtpVerifyVM { Email = email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyLoginOtp(OtpVerifyVM vm)
    {
        var email = HttpContext.Session.GetString(LoginOtpEmail);
        var userIdText = HttpContext.Session.GetString(LoginOtpUserId);
        if (string.IsNullOrWhiteSpace(email) || !Guid.TryParse(userIdText, out var userId)) return RedirectToAction(nameof(Login));
        ModelState.Remove(nameof(vm.Email));
        vm.Email = email;
        if (!ModelState.IsValid) return View(vm);
        if (!otpEmail.VerifyCode(email, vm.Code, "login"))
        {
            ModelState.AddModelError(nameof(vm.Code), "Mã OTP không đúng, hết hạn hoặc đã vượt quá số lần thử.");
            return View(vm);
        }

        var user = store.Read<User>(UsersFile).FirstOrDefault(u => u.Id == userId && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (user is null || user.IsLocked)
        {
            ClearLoginOtpSession();
            ModelState.AddModelError(string.Empty, "Tài khoản không còn khả dụng. Hãy đăng nhập lại.");
            return View(vm);
        }

        var returnUrl = HttpContext.Session.GetString(LoginOtpReturnUrl);
        ClearLoginOtpSession();
        await SignIn(user);
        return AfterLogin(user, returnUrl);
    }

    private void ClearLoginOtpSession()
    {
        HttpContext.Session.Remove(LoginOtpEmail);
        HttpContext.Session.Remove(LoginOtpUserId);
        HttpContext.Session.Remove(LoginOtpReturnUrl);
    }

    private IActionResult AfterLogin(User user, string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
        ? Redirect(returnUrl)
        : user.Role == "Admin" ? RedirectToAction("Dashboard", "Admin") : RedirectToAction("Index", "Home");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private Task SignIn(User user) => HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        AccountIdentity.Create(user),
        new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMonths(3)
        });

    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }
}
