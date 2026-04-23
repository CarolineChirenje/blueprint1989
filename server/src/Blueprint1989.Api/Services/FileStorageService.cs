using Blueprint1989.Api.Data;
using Blueprint1989.Api.Models;

namespace Blueprint1989.Api.Services;

public interface IFileStorageService
{
    Task<(int fileId, string contentType)> UploadAsync(IFormFile file, string folder, int userId);
    Task<UploadedFile?> GetByIdAsync(int id);
}

public class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB

    private readonly ApplicationDbContext _db;

    public FileStorageService(ApplicationDbContext db) => _db = db;

    public async Task<(int fileId, string contentType)> UploadAsync(IFormFile file, string folder, int userId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file provided.");

        if (file.Length > MaxFileSize)
            throw new ArgumentException("File exceeds the 5 MB limit.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException("Only jpg, png, webp images and pdf documents are allowed.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var entity = new UploadedFile
        {
            FileName = $"{Guid.NewGuid()}{ext}",
            ContentType = file.ContentType,
            Data = ms.ToArray(),
            SizeBytes = file.Length,
            Folder = folder,
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = userId
        };

        _db.UploadedFiles.Add(entity);
        await _db.SaveChangesAsync();

        return (entity.Id, entity.ContentType);
    }

    public async Task<UploadedFile?> GetByIdAsync(int id)
    {
        return await _db.UploadedFiles.FindAsync(id);
    }
}
