using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using OutfitStudio.Models;
using OutfitStudio.Services;

internal static class PaymentChecks
{
    public static async Task RunAsync(AppStore store, QrPaymentService payments, HttpClient client, HttpClient stranger,
        Func<HttpClient, string, string, Dictionary<string, string>, Task<HttpResponseMessage>> post, Action<bool, string> assert,
        int productId, string size)
    {
        Task<HttpResponseMessage> Post(string path, string form, Dictionary<string, string> body) => post(client, path, form, body);
        Dictionary<string, string> Settings(string account = "0000000000000") => new()
        {
            ["Enabled"] = "true", ["BankBin"] = "970415", ["BankName"] = "Synthetic Bank",
            ["AccountNumber"] = account, ["AccountName"] = "QR TEST ONLY"
        };
        async Task Add() => await Post("/Cart/Add", "/Product/Details/" + productId, new() { ["productId"] = productId.ToString(), ["size"] = size, ["quantity"] = "1" });
        Dictionary<string, string> Checkout() => new()
        {
            ["FullName"] = "Synthetic QR customer", ["Phone"] = "0900000000", ["Address"] = "Test only", ["PaymentMethod"] = "QR Pay",
            ["Total"] = "1", ["QrPayment.Status"] = "2", ["QrPayment.AccountNumber"] = "ATTACKER", ["QrPayAvailable"] = "true"
        };
        var initialOrders = store.Read<Order>("orders.json").Count;
        await Add();
        var disabledPage = await client.GetStringAsync("/Order/Checkout");
        assert(!disabledPage.Contains("value=\"QR Pay\""), "Unconfigured QR Pay is hidden at checkout");
        var initialStock = store.All<Product>("products").First(p => p.Id == productId).StockOf(size);
        await Post("/Order/Checkout", "/Order/Checkout", Checkout());
        assert(store.Read<Order>("orders.json").Count == initialOrders && store.All<Product>("products").First(p => p.Id == productId).StockOf(size) == initialStock, "Forged QR checkout cannot bypass missing receiving account or deduct stock");
        assert(!disabledPage.Contains("value=\"Chuyển khoản\""), "Standalone bank transfer option removed");
        var transfer = Checkout();
        transfer["PaymentMethod"] = "Chuyển khoản";
        await Post("/Order/Checkout", "/Order/Checkout", transfer);
        assert(store.Read<Order>("orders.json").Count == initialOrders && store.All<Product>("products").First(p => p.Id == productId).StockOf(size) == initialStock, "Removed bank transfer method cannot create order or deduct stock");
        var invalidSettings = Settings(); invalidSettings["BankBin"] = "bad";
        await Post("/PaymentSettings", "/PaymentSettings", invalidSettings);
        assert(!payments.IsConfigured, "Invalid bank configuration cannot enable QR Pay");
        var noCsrf = await client.PostAsync("/PaymentSettings", new FormUrlEncodedContent(Settings()));
        assert(noCsrf.StatusCode == HttpStatusCode.BadRequest, "Payment settings require CSRF");
        await Post("/PaymentSettings", "/PaymentSettings", Settings());
        assert(payments.IsConfigured, "Admin configures receiving account without gateway secrets");
        assert((await client.GetStringAsync("/Order/Checkout")).Contains("value=\"QR Pay\""), "Configured QR Pay appears at checkout");
        store.Modify<User>("users.json", users => users.Single().Role = "User");
        assert((await client.GetAsync("/PaymentSettings")).Headers.Location?.OriginalString?.Contains("AccessDenied") == true, "Customer cannot open merchant payment settings");
        var checkout = await Post("/Order/Checkout", "/Order/Checkout", Checkout());
        var order = store.Read<Order>("orders.json").OrderByDescending(o => o.Id).First();
        assert(checkout.Headers.Location?.OriginalString?.EndsWith("/Order/Detail/" + order.Id) == true, "QR checkout redirects directly to order payment page");
        assert(order.PaymentMethod == QrPaymentService.Method && order.QrPayment?.Status == QrPaymentStatus.Unpaid && order.QrPayment.AccountNumber == Settings()["AccountNumber"] && order.Total > 1, "QR order ignores client amount, paid status and destination account");
        assert(order.StockDeducted && store.All<Product>("products").First(p => p.Id == productId).StockOf(size) == initialStock - 1, "QR checkout deducts inventory exactly once");
        var url = QrPaymentService.ImageUrl(order)!;
        var uri = new Uri(url);
        assert(uri.Scheme == "https" && uri.Host == "img.vietqr.io" && uri.AbsolutePath.EndsWith("-compact2.svg") &&
            uri.Query.Contains("amount=" + order.Total.ToString("0", System.Globalization.CultureInfo.InvariantCulture)) && uri.Query.Contains("addInfo=" + order.QrPayment!.Reference), "QR URL uses official SVG format, stored total and unique reference");
        assert(!url.Contains("0900000000") && !url.Contains("Synthetic%20QR%20customer") && !url.Contains("Test%20only"), "QR image request excludes customer phone, name and shipping address");
        var detail = await client.GetStringAsync("/Order/Detail/" + order.Id);
        assert(detail.Contains("data-qr-image") && detail.Contains("ReportTransfer") && detail.Contains("qr-payment.js"), "Customer payment page renders QR, details and report button");
        assert((await stranger.GetAsync("/Order/Detail/" + order.Id)).StatusCode == HttpStatusCode.Redirect, "Anonymous visitor cannot view customer QR order");
        var forbiddenConfirm = await Post("/Admin/ConfirmQrPayment", "/Order/Detail/" + order.Id, new() { ["id"] = order.Id.ToString(), ["received"] = "true", ["transactionReference"] = "FORGED" });
        assert(forbiddenConfirm.Headers.Location?.OriginalString?.Contains("AccessDenied") == true && store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.Status == QrPaymentStatus.Unpaid, "Customer cannot mark order paid through admin endpoint");
        await Post("/Order/ReportTransfer", "/Order/Detail/" + order.Id, new() { ["id"] = order.Id.ToString() });
        await Post("/Order/ReportTransfer", "/Order/Detail/" + order.Id, new() { ["id"] = order.Id.ToString() });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.Status == QrPaymentStatus.CustomerReported, "Customer transfer report is idempotent and never marks paid");
        // An authenticated second owner is represented by a temporary principal email change in isolated user data.
        var user = store.Read<User>("users.json").Single();
        var otherOrder = new Order { Id = order.Id + 100, UserEmail = "different-owner@example.test", PaymentMethod = QrPaymentService.Method, Subtotal = order.Subtotal, ShippingFee = order.ShippingFee, Total = order.Total, QrPayment = payments.Create(order.Id + 100) };
        store.Modify<Order>("orders.json", orders => orders.Add(otherOrder));
        assert((await client.GetAsync("/Order/Detail/" + otherOrder.Id)).StatusCode == HttpStatusCode.NotFound &&
            (await Post("/Order/ReportTransfer", "/Order/Detail/" + order.Id, new() { ["id"] = otherOrder.Id.ToString() })).StatusCode == HttpStatusCode.NotFound,
            "Authenticated customer cannot view or report another owner's QR order");
        store.Modify<User>("users.json", users => users.Single().Role = "Admin");
        await Post("/PaymentSettings", "/PaymentSettings", Settings("0000000000001"));
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.AccountNumber == "0000000000000", "Changing receiving account preserves existing order snapshot");
        await Post("/Admin/UpdateOrderStatus", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["status"] = OrderStatus.Confirmed });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).Status == OrderStatus.Pending, "Unpaid QR order cannot advance to fulfillment");
        await Post("/Admin/ConfirmQrPayment", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["transactionReference"] = "QR-TEST-TRANSACTION" });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.Status == QrPaymentStatus.CustomerReported, "Manual confirmation requires explicit receipt acknowledgement");
        await Post("/Admin/ReopenQrPayment", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString() });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.Status == QrPaymentStatus.Unpaid, "Admin can request transfer recheck without claiming receipt");
        await Post("/Admin/ConfirmQrPayment", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["received"] = "true", ["transactionReference"] = "QR-TEST-TRANSACTION" });
        var paid = store.Read<Order>("orders.json").First(o => o.Id == order.Id);
        assert(paid.QrPayment!.Status == QrPaymentStatus.Paid && paid.QrPayment.PaidAt.HasValue && paid.QrPayment.BankTransactionReference == "QR-TEST-TRANSACTION" && QrPaymentService.ImageUrl(paid) is null, "Admin bank reconciliation marks paid with timestamp and audit reference, hides QR");
        var paidAt = paid.QrPayment.PaidAt;
        await Post("/Admin/ConfirmQrPayment", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["received"] = "true", ["transactionReference"] = "DIFFERENT" });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).QrPayment!.PaidAt == paidAt, "Duplicate confirmation cannot rewrite payment audit");
        assert(payments.Confirm(otherOrder.Id, "QR-TEST-TRANSACTION", "Synthetic") != "" && store.Read<Order>("orders.json").First(o => o.Id == otherOrder.Id).QrPayment!.Status == QrPaymentStatus.Unpaid, "One bank transaction cannot confirm two orders");
        await Post("/Admin/UpdateOrderStatus", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["status"] = OrderStatus.Confirmed });
        assert(store.Read<Order>("orders.json").First(o => o.Id == order.Id).Status == OrderStatus.Confirmed, "Paid QR order can advance to fulfillment");
        await Post("/Admin/UpdateOrderStatus", "/Admin/OrderDetails/" + order.Id, new() { ["id"] = order.Id.ToString(), ["status"] = OrderStatus.Cancelled });
        var cancelled = store.Read<Order>("orders.json").First(o => o.Id == order.Id);
        assert(cancelled.QrPayment!.Status == QrPaymentStatus.Paid && QrPaymentService.ImageUrl(cancelled) is null &&
            store.All<Product>("products").First(p => p.Id == productId).StockOf(size) == initialStock, "Cancellation preserves receipt audit, hides QR and restores inventory");
        assert((await client.GetStringAsync("/Order/Detail/" + order.Id)).Contains("qr-notice"), "Cancelled paid order displays reconciliation/refund guidance");
        var cancelUnpaid = new Order { Id = otherOrder.Id + 100, UserEmail = user.Email, PaymentMethod = QrPaymentService.Method, Subtotal = order.Subtotal, ShippingFee = order.ShippingFee, Total = order.Total, QrPayment = payments.Create(otherOrder.Id + 100), Status = OrderStatus.Cancelled };
        store.Modify<Order>("orders.json", orders => orders.Add(cancelUnpaid));
        assert(payments.Report(cancelUnpaid.Id, user.Email) != "" && payments.Confirm(cancelUnpaid.Id, "CANCELLED-TX", "Synthetic") != "" && QrPaymentService.ImageUrl(cancelUnpaid) is null, "Cancelled unpaid QR order cannot accept report, confirmation or QR display");
        assert(!QrPaymentService.ValidAmount(1.5m) && !QrPaymentService.ValidAmount(0) && !QrPaymentService.ValidAmount(10000000000000m), "QR amount rejects fractional, zero and oversized VND values");
        await Post("/PaymentSettings", "/PaymentSettings", new() { ["Enabled"] = "false" });
        assert(!payments.IsConfigured && QrPaymentService.ImageUrl(otherOrder) is not null, "Disabling QR hides it for new orders while preserving existing unpaid QR");
        // Anonymous CSRF-free state changes never reach payment service.
        assert((await stranger.PostAsync("/Order/ReportTransfer", new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = otherOrder.Id.ToString() }))).StatusCode == HttpStatusCode.Redirect, "Anonymous transfer report requires login");
    }
}


