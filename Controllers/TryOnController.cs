using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OutfitStudio.Models;
using OutfitStudio.Services;

namespace OutfitStudio.Controllers;

public sealed class TryOnController(VirtualTryOnService service) : Controller
{
    public IActionResult Index() => View(new VirtualTryOnPage { IsConfigured = service.IsConfigured, IsAuthenticated = User.Identity?.IsAuthenticated == true, Products = service.AvailableProducts() });

    [Authorize, HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(21 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 21 * 1024 * 1024, MemoryBufferThreshold = 21 * 1024 * 1024)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Generate([FromForm] VirtualTryOnRequest request, IFormFile? person, IFormFile? mask, List<IFormFile>? garments, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new
            {
                message = "Thông tin thử đồ không hợp lệ."
            });
        try
        {
            VirtualTryOnService.Validate(request, garments?.Count ?? 0);
            if (person is null)
                throw new ImageProcessingException("Hãy chọn ảnh của bạn để thử đồ.", 400);
            var target = await CloudflareImageService.ReadImageAsync(person, cancellationToken);
            var editMask = mask is null ? null : await CloudflareImageService.ReadImageAsync(mask, cancellationToken);
            var references = new List<ImageInput>();
            foreach (var file in garments ?? [])
                references.Add(await CloudflareImageService.ReadImageAsync(file, cancellationToken));
            var png = await service.GenerateAsync(request, target, editMask, references, cancellationToken);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(png, "image/png");
        }
        catch (ImageProcessingException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return StatusCode(499); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return StatusCode(500, new { message = "Không đọc được ảnh sản phẩm. Kiểm tra danh mục rồi thử lại." }); }
    }
}
