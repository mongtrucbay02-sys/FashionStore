using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.ViewModels;

public class RegisterVM
{
    [Required(ErrorMessage = "Nhập họ tên")]
    [StringLength(200)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập mã OTP"), RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số")]
    [Display(Name = "Mã OTP")]
    public string OtpCode { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu"), MinLength(6, ErrorMessage = "Tối thiểu 6 ký tự")]
    [StringLength(128)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [Compare("Password", ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";
}

