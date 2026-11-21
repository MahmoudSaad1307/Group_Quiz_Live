using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuizLive.Application.Abstractions;

namespace QuizLive.Api.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "Admin")]
public sealed class UploadsController(
    IImageProcessor imageProcessor,
    IFileStorage fileStorage) : ControllerBase
{
    private const long MaxBytes = 2 * 1024 * 1024;

    [HttpPost("image")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxBytes)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0 || file.Length > MaxBytes)
        {
            return Problem(
                title: "Invalid file",
                detail: "Image file must be between 1 byte and 2 MB.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var processed = await imageProcessor.ProcessAsync(stream, ct);
            var url = await fileStorage.SaveAsync(processed.Data, processed.Extension, ct);
            return Ok(new { url });
        }
        catch (InvalidOperationException)
        {
            return Problem(
                title: "Unsupported image",
                detail: "The uploaded file is not a valid supported image.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
