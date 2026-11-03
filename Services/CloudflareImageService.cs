using System.Net;
using System.Net.Http.Headers;
using SkiaSharp;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OutfitStudio.Services;

public sealed class ImageProcessingException(string message, int statusCode = 502) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed record ImageInput(byte[] Bytes, string MediaType);

public sealed class CloudflareImageService(IHttpClientFactory clients, IOptions<CloudflareAIOptions> options, IConfiguration configuration)
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;
    private readonly SemaphoreSlim gate = new(1, 1);
    private string ApiKey => string.IsNullOrWhiteSpace(options.Value.ApiKey) ? configuration["CLOUDFLARE_API_TOKEN"] ?? "" : options.Value.ApiKey;
    private string AccountId => (string.IsNullOrWhiteSpace(options.Value.AccountId) ? configuration["CLOUDFLARE_ACCOUNT_ID"] ?? "" : options.Value.AccountId).Trim();
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && AccountId.Length == 32 && AccountId.All(Uri.IsHexDigit);
    private string Endpoint(string model) => "https://api.cloudflare.com/client/v4/accounts/" + AccountId + "/ai/run/" + string.Join("/", model.Split('/').Select(part => Uri.EscapeDataString(part).Replace("%40", "@")));
    private static JsonElement Unwrap(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            throw new ImageProcessingException("Cloudflare chưa trả kết quả hợp lệ. Kiểm tra quyền Workers AI, mô hình và hạn mức.");
        return result;
    }
    private static SKBitmap DecodeImage(ImageInput input)
    {
        using var data = SKData.CreateCopy(input.Bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width < 1 || codec.Info.Height < 1 || (long)codec.Info.Width * codec.Info.Height > 20_000_000)
            throw new ImageProcessingException("Ảnh không hợp lệ hoặc vượt 20 megapixel.", 400);
        return SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)) ?? throw new ImageProcessingException("Không giải mã được ảnh.", 400);
    }
    private static byte[] ReferencePng(ImageInput input, ImageInput? guideMask = null)
    {
        using var decoded = DecodeImage(input);
        if (guideMask is not null)
        {
            using var maskImage = DecodeImage(guideMask);
            if (maskImage.Width != decoded.Width || maskImage.Height != decoded.Height)
                throw new ImageProcessingException("Vùng tô phải cùng kích thước ảnh người.", 400);
            var pixels = decoded.Pixels;
            var maskPixels = maskImage.Pixels;
            for (var i = 0; i < pixels.Length; i++)
            {
                var strength = (255 - maskPixels[i].Alpha) / 510f;
                var pixel = pixels[i];
                pixels[i] = new SKColor((byte)(pixel.Red * (1 - strength) + 255 * strength), (byte)(pixel.Green * (1 - strength)), (byte)(pixel.Blue * (1 - strength)), 255);
            }
            decoded.Pixels = pixels;
        }
        var scale = Math.Min(1d, 512d / Math.Max(decoded.Width, decoded.Height));
        using var resized = decoded.Resize(new SKImageInfo(Math.Max(1, (int)Math.Round(decoded.Width * scale)), Math.Max(1, (int)Math.Round(decoded.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null)
            throw new ImageProcessingException("Không chuẩn hóa được ảnh.", 400);
        using var png = resized.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
    private static (int Width, int Height) OutputSize(string size, ImageInput first)
    {
        if (size == "1024x1024")
            return (512, 512);
        if (size == "1024x1536")
            return (512, 768);
        if (size == "1536x1024")
            return (768, 512);
        using var image = DecodeImage(first);
        var scale = Math.Min(1d, 768d / Math.Max(image.Width, image.Height));
        return (Math.Clamp((int)Math.Round(image.Width * scale / 16) * 16, 128, 768), Math.Clamp((int)Math.Round(image.Height * scale / 16) * 16, 128, 768));
    }

    public static async Task<ImageInput> ReadImageAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ImageProcessingException("Hãy chọn một ảnh quần áo để xử lý.", 400);
        if (file.Length > MaxUploadBytes)
            throw new ImageProcessingException("Ảnh quá lớn. Vui lòng chọn ảnh tối đa 5 MB.", 400);
        await using var source = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > MaxUploadBytes)
                throw new ImageProcessingException("Ảnh quá lớn. Vui lòng chọn ảnh tối đa 5 MB.", 400);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        var bytes = output.ToArray();
        var type = DetectMediaType(bytes);
        if (type is null)
            throw new ImageProcessingException("File không phải ảnh JPG, PNG hoặc WEBP hợp lệ. Không hỗ trợ SVG, GIF hoặc HEIC.", 400);
        return new ImageInput(bytes, type);
    }

    public static string? DetectMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 33 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
            return "image/png";
        if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
            return "image/jpeg";
        if (bytes.Length >= 20 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    public async Task<byte[]> EditImagesAsync(IReadOnlyList<ImageInput> images, string prompt, string size, CancellationToken cancellationToken, ImageInput? mask = null)
    {
        if (images.Count is < 1 or > 4 || size is not ("1024x1024" or "1024x1536" or "1536x1024" or "auto"))
            throw new ArgumentException("Invalid image edit inputs.");
        if (!IsConfigured)
            throw new ImageProcessingException("Dịch vụ AI chưa được kết nối. Cấu hình CLOUDFLARE_API_TOKEN và CLOUDFLARE_ACCOUNT_ID trên máy chủ rồi khởi động lại ứng dụng.", 503);
        if (!await gate.WaitAsync(0, cancellationToken))
            throw new ImageProcessingException("Studio đang xử lý một ảnh khác. Vui lòng chờ ảnh đó hoàn tất rồi thử lại.", 429);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(options.Value.Model));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey.Trim());
            using var body = new MultipartFormDataContent();
            void AddImage(byte[] bytes, int index)
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                body.Add(content, $"input_image_{index}", $"reference-{index}.png");
            }
            for (var i = 0; i < images.Count; i++)
                AddImage(ReferencePng(images[i]), i);
            if (mask is not null)
            {
                if (images.Count >= 4)
                    throw new ImageProcessingException("Tối đa ba ảnh tham chiếu khi có vùng tô.", 400);
                AddImage(ReferencePng(images[0], mask), images.Count);
                prompt += " The LAST image is a red-tinted edit-region GUIDE copied from image 1, not a garment. Change only the red-highlighted clothing area. Do not reproduce red tint in the result. Use image 1 for the original colors, scene, identity and framing. Return one aligned final image.";
            }
            var (width, height) = OutputSize(size, images[0]);
            body.Add(new StringContent(prompt), "prompt");
            body.Add(new StringContent("20"), "steps");
            body.Add(new StringContent(width.ToString(System.Globalization.CultureInfo.InvariantCulture)), "width");
            body.Add(new StringContent(height.ToString(System.Globalization.CultureInfo.InvariantCulture)), "height");
            request.Content = body;
            using var response = await clients.CreateClient("CloudflareImageService").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Khóa API chưa hợp lệ. Vui lòng kiểm tra cấu hình trên máy chủ.",
                    HttpStatusCode.Forbidden or HttpStatusCode.NotFound => "Tài khoản API chưa được cấp quyền dùng mô hình ảnh đã cấu hình.",
                    HttpStatusCode.TooManyRequests => "Dịch vụ AI đang giới hạn lượt xử lý hoặc tài khoản đã hết hạn mức. Kiểm tra tài khoản API rồi thử lại.",
                    HttpStatusCode.BadRequest => "AI chưa thể xử lý yêu cầu ảnh này. Kiểm tra ảnh và vùng chỉnh sửa rồi thử lại.",
                    _ => "Dịch vụ AI tạm thời không phản hồi. Vui lòng thử lại sau."
                };
                throw new ImageProcessingException(message, response.StatusCode == HttpStatusCode.TooManyRequests ? 429 : 502);
            }
            using var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var data = Unwrap(result.RootElement);
            if (!data.TryGetProperty("image", out var encoded) || encoded.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(encoded.GetString()))
                throw new ImageProcessingException("Cloudflare chưa trả về ảnh kết quả. Vui lòng thử lại.");
            var text = encoded.GetString()!;
            if (text.Length > 28 * 1024 * 1024)
                throw new ImageProcessingException("Ảnh kết quả vượt giới hạn lưu trữ.");
            var bytes = Convert.FromBase64String(text);
            if (DetectMediaType(bytes) is not ("image/png" or "image/jpeg" or "image/webp"))
                throw new ImageProcessingException("Ảnh kết quả không đúng định dạng ảnh hợp lệ.");
            try
            {
                using var decoded = DecodeImage(new(bytes, DetectMediaType(bytes)!));
                using var normalized = decoded.Encode(SKEncodedImageFormat.Png, 100);
                return normalized.ToArray();
            }
            catch (ImageProcessingException) { throw new ImageProcessingException("Cloudflare trả ảnh lỗi hoặc vượt giới hạn xử lý."); }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ImageProcessingException("Thời gian xử lý ảnh đã hết. Vui lòng thử lại sau.", 504);
        }
        catch (HttpRequestException)
        {
            throw new ImageProcessingException("Không thể kết nối dịch vụ AI. Kiểm tra kết nối mạng của máy chủ.", 502);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            throw new ImageProcessingException("Dịch vụ AI trả về dữ liệu ảnh không hợp lệ.");
        }
        finally { gate.Release(); }
    }
}
