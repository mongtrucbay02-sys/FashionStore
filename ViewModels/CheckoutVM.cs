using System.ComponentModel.DataAnnotations;
using OutfitStudio.Models;

namespace OutfitStudio.ViewModels;

public class CheckoutVM
{
    [Required(ErrorMessage = "Nhập họ tên")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập số điện thoại")]
    [RegularExpression(@"^(0|\+84)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Nhập địa chỉ")]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; } = "";

    [Required(ErrorMessage = "Chọn phương thức thanh toán")]
    public string PaymentMethod { get; set; } = "COD";

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool QrPayAvailable
    {
        get; set;
    }

    // chỉ để hiển thị
    public List<CartItem> Items { get; set; } = new();
    public decimal Subtotal
    {
        get; set;
    }
    public decimal ShippingFee
    {
        get; set;
    }
    public decimal Total => Subtotal + ShippingFee;
}
