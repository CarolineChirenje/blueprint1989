using System.Security;

namespace Divvy.Api.Services;

public interface IFileStorageService
{
    Task<string> UploadAsync(IFormFile file, string folder);
}

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp" };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    private readonly IWebHostEnvironment _env;

    public FileStorageService(IWebHostEnvironment env) => _env = env;

    public async Task<string> UploadAsync(IFormFile file, string folder)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file provided.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("File exceeds the 5 MB limit.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException("Only jpg, png, and webp images are allowed.");

        // Sanitise folder to prevent path traversal
        var safeName = SanitisePath(folder);
        var dirPath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", safeName);
        Directory.CreateDirectory(dirPath);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(dirPath, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/{safeName}/{fileName}";
    }

    private static string SanitisePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "general";

        // Strip anything that isn't alphanumeric, dash, or underscore
        var safe = new string(input.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
        if (string.IsNullOrEmpty(safe))
            throw new SecurityException("Invalid folder name.");

        return safe;
    }
}
