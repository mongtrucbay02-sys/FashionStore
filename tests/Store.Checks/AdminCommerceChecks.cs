using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using OutfitStudio.Models;
using OutfitStudio.Services;

internal static class AdminCommerceChecks
{
    private sealed record PaymentState(bool Paid);
    public static async Task RunAsync(WebApplication app, Func<HttpClient> client,
        Func<HttpClient, string, string, Dictionary<string, string>, Task<HttpResponseMessage>> post,
        Action<bool, string> check)
    {
        var store = app.Services.GetRequiredService<AppStore>();
        var inventory = app.Services.GetRequiredService<InventoryService>();
        var cache = app.Services.GetRequiredService<IMemoryCache>();
        const string otp = "815093";
        const string password = "Synthetic!2026";

        void SeedOtp(string email, string purpose) => cache.Set($"account:otp:{purpose}:{email}", new OtpCodeEntry
        {
            CodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp))),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        }, TimeSpan.FromMinutes(5));

        async Task<User> Register(HttpClient browser, string email)
        {
            SeedOtp(email, "register");
            var response = await post(browser, "/Account/Register", "/Account/Register", new()
            {
                ["FullName"] = "Synthetic Buyer", ["Email"] = email, ["Password"] = password,
                ["ConfirmPassword"] = password, ["OtpCode"] = otp
            });
            check(response.StatusCode == HttpStatusCode.Redirect, $"Registered independent session for {email}");
            return store.Read<User>("users.json").Single(u => u.Email == email);
        }

        using var buyerA = client();
        using var buyerB = client();
        using var admin = client();
        var userA = await Register(buyerA, "buyer-a@example.test");
        var userB = await Register(buyerB, "buyer-b@example.test");
        var root = await Register(admin, "root@example.test");
        store.Modify<User>("users.json", users => users.Single(u => u.Id == root.Id).Role = "Admin");

        var product = store.All<Product>("products").First(p => p.Active && p.Audience == "men");
        var size = product.AvailableSizes[0];
        inventory.SetStock(product.Id, size, 1, "", "Test");
        var detail = "/Product/Details/" + product.Id;

        async Task SetVisibility(bool active) =>
            check((await post(admin, "/Admin/SetProductVisibility/" + product.Id, "/Admin/Products",
                new() { ["active"] = active ? "true" : "false" })).StatusCode == HttpStatusCode.Redirect, "Admin changed product visibility");

        await SetVisibility(false);
        check((await buyerA.GetAsync(detail)).StatusCode == HttpStatusCode.NotFound, "Hidden product returns 404 in storefront");
        await SetVisibility(true);
        foreach (var buyer in new[] { buyerA, buyerB })
            check((await post(buyer, "/Cart/Add", detail, new()
            {
                ["productId"] = product.Id.ToString(), ["size"] = size, ["quantity"] = "1"
            })).StatusCode == HttpStatusCode.Redirect, "Buyer adds the last unit to their own cart");

        app.Services.GetRequiredService<QrPaymentService>().Save(new()
        {
            Enabled = true, BankBin = "970436", BankName = "Test Bank", AccountNumber = "12345678", AccountName = "TEST"
        });
        Task<HttpResponseMessage> Checkout(HttpClient browser) => post(browser, "/Order/Checkout", "/Order/Checkout", new()
        {
            ["FullName"] = "Synthetic Buyer", ["Phone"] = "0900000000",
            ["Address"] = "Test only", ["PaymentMethod"] = QrPaymentService.Method
        });
        var results = await Task.WhenAll(Checkout(buyerA), Checkout(buyerB));
        var order = store.Read<Order>("orders.json").Single();
        check(results.Count(r => r.Headers.Location?.OriginalString.StartsWith("/Order/Detail/") == true) == 1 &&
            results.Count(r => r.Headers.Location?.OriginalString.StartsWith("/Cart") == true) == 1 &&
            order.StockDeducted && order.Items.Single().Quantity == 1 &&
            store.All<Product>("products").Single(p => p.Id == product.Id).StockOf(size) == 0,
            "Two simultaneous checkouts produce one order, one stock rejection, and zero remaining units");

        var winner = order.UserEmail == userA.Email ? buyerA : buyerB;
        var otherBuyer = winner == buyerA ? buyerB : buyerA;
        var statusUrl = "/Order/PaymentStatus/" + order.Id;
        check((await otherBuyer.GetAsync(statusUrl)).StatusCode == HttpStatusCode.NotFound,
            "Payment status is private to the order owner");
        check(!(await winner.GetFromJsonAsync<PaymentState>(statusUrl))!.Paid &&
            (await winner.GetStringAsync("/Order/Detail/" + order.Id)).Contains(statusUrl),
            "Pending QR page polls the order status");

        var qr = order.QrPayment!;
        async Task<HttpResponseMessage> Transfer(decimal amount, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/sepay/webhook");
            request.Headers.TryAddWithoutValidation("Authorization", "Apikey " + key);
            request.Content = JsonContent.Create(new
            {
                accountNumber = qr.AccountNumber, content = qr.Reference + " TRANSFER",
                transferType = "in", transferAmount = amount, referenceCode = "BANK123456"
            });
            return await admin.SendAsync(request);
        }
        Order ReadOrder() => store.Read<Order>("orders.json").Single();
        check((await Transfer(order.Total, "invalid")).StatusCode == HttpStatusCode.Unauthorized, "Webhook requires correct API key");
        await Transfer(order.Total - 1, "synthetic-webhook-key");
        check(ReadOrder().QrPayment!.Status == QrPaymentStatus.Unpaid, "Wrong transfer amount leaves order unpaid");
        await Transfer(order.Total, "synthetic-webhook-key");
        var paid = ReadOrder().QrPayment!;
        check((await winner.GetFromJsonAsync<PaymentState>(statusUrl))!.Paid &&
            !(await winner.GetStringAsync("/Order/Detail/" + order.Id)).Contains(statusUrl),
            "Confirmed transfer appears in live status and the paid page stops polling");
        await Transfer(order.Total, "synthetic-webhook-key");
        check(paid.Status == QrPaymentStatus.Paid && paid.ConfirmedBy == "SePay" && paid.PaidAt == ReadOrder().QrPayment!.PaidAt,
            "Valid SePay webhook marks order paid exactly once");

        store.Modify<Order>("orders.json", orders => orders.Single().Status = OrderStatus.Completed);
        var dashboard = WebUtility.HtmlDecode(await admin.GetStringAsync("/Admin/Dashboard"));
        check(dashboard.Contains("14 ngày gần nhất") && dashboard.Contains("Sản phẩm bán chạy nhất") && dashboard.Contains(product.Name),
            "Dashboard renders daily chart and completed-order best seller");

        SeedOtp(userA.Email, "reset-password:" + userA.Id);
        var reset = await post(buyerA, "/Account/ForgotPassword", "/Account/ForgotPassword", new()
        {
            ["Email"] = userA.Email, ["Code"] = otp, ["Password"] = "NewSynthetic!2026"
        });
        var updated = store.Read<User>("users.json").Single(u => u.Id == userA.Id);
        check(reset.StatusCode == HttpStatusCode.Redirect &&
            new PasswordHasher<User>().VerifyHashedPassword(updated, updated.PasswordHash, "NewSynthetic!2026") != PasswordVerificationResult.Failed &&
            (await buyerA.GetAsync("/Order/History")).StatusCode == HttpStatusCode.Redirect,
            "Password reset updates password and revokes the old session");

        await post(admin, "/Admin/DeleteUser/" + root.Id, "/Admin/Users", new());
        check(!store.Read<User>("users.json").Single(u => u.Id == root.Id).IsDeleted, "Root admin deletion rejected");
        await post(admin, "/Admin/DeleteUser/" + userB.Id, "/Admin/Users", new());
        check(store.Read<User>("users.json").Single(u => u.Id == userB.Id).IsDeleted &&
            (await buyerB.GetAsync("/Order/History")).StatusCode == HttpStatusCode.Redirect, "Deleting customer revokes active session");
        await post(admin, "/Admin/ChangeRole/" + userA.Id, "/Admin/Users", new() { ["role"] = "Admin" });
        await post(admin, "/Admin/DeleteUser/" + userA.Id, "/Admin/Users", new());
        check(store.Read<User>("users.json").Single(u => u.Id == userA.Id).IsDeleted, "Root admin can delete a secondary admin");
    }
}
