using System.ComponentModel.DataAnnotations;

namespace OutfitStudio.ViewModels;

public sealed class ResetPasswordVM
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, RegularExpression("[0-9]{6}")]
    public string Code { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = "";
}
