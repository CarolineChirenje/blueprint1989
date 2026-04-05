using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.Services;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorageService _storage;

    public FilesController(IFileStorageService storage) => _storage = storage;

    /// <summary>Uploads an image file (jpg/png/webp, max 5 MB) and returns its URL.</summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string folder = "proofs")
    {
        try
        {
            var url = await _storage.UploadAsync(file, folder);
            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
