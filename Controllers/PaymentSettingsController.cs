using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PaymentSettingsController(QrPaymentService payments) : Controller
{
    [HttpGet] public IActionResult Index() => View(payments.Settings);

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Index(QrPaymentSettings model)
    {
        if (ModelState.TryGetValue(nameof(model.Enabled), out var enabled) && enabled.Errors.Count > 0)
            return View(model);
        model.BankBin = (model.BankBin ?? "").Trim();
        model.BankName = (model.BankName ?? "").Trim();
        model.AccountNumber = (model.AccountNumber ?? "").Trim();
        model.AccountName = (model.AccountName ?? "").Trim();
        ModelState.Clear();
        if (model.Enabled && !TryValidateModel(model))
            return View(model);
        // Disabled settings retain only bounded, valid values, preventing accidental unsafe snapshots.
        if (!model.Enabled)
            model = new QrPaymentSettings();
        payments.Save(model);
        TempData["Success"] = model.Enabled ? "Đã bật QR Pay. Hãy kiểm tra lại tài khoản nhận tiền trước khi nhận đơn." : "Đã tắt QR Pay cho đơn mới. Đơn QR đã tạo giữ nguyên thông tin thanh toán.";
        return RedirectToAction(nameof(Index));
    }
}
