using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.ViewModels;

public class LoginVM
{
    [Required(ErrorMessage = "Nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";
}
