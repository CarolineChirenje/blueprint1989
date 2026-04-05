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

    public FilesController(IFileStorageService storage) => _storage = storage;

    /// <summary>Uploads a file (jpg/png/webp/pdf, max 5 MB), stores bytes in DB, returns its URL.</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "proofs")
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var (fileId, _) = await _storage.UploadAsync(file, folder, userId);

            var url = $"/api/files/{fileId}";
            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
