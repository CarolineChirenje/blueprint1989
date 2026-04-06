using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileStorageService storage, ILogger<FilesController> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Uploads a file (jpg/png/webp/pdf, max 5 MB), stores bytes in DB, returns its URL.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(6 * 1024 * 1024)] // 6 MB to allow overhead beyond the 5 MB file limit
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "proofs")
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (fileId, _) = await _storage.UploadAsync(file, folder, userId);

            var url = $"/api/files/{fileId}";
            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File upload failed for folder {Folder}", folder);
            return StatusCode(500, new { message = "File upload failed. Please try again." });
        }
    }

    /// <summary>Serves an uploaded file by ID.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int id)
    {
        var file = await _storage.GetByIdAsync(id);
        if (file == null)
            return NotFound();

        return File(file.Data, file.ContentType, file.FileName);
    }
}
