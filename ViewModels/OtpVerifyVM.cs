using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.ViewModels;

public class OtpVerifyVM
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập mã OTP"), RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã OTP gồm 6 chữ số")]
    [Display(Name = "Mã OTP")]
    public string Code { get; set; } = "";
}
