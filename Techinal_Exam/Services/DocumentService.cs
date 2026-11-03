using Microsoft.EntityFrameworkCore;
using Techinal_Exam.Data;
using Techinal_Exam.Models;

namespace Techinal_Exam.Services;

public class DocumentService : IDocumentService
{
    // Allowed types: extension -> (content type, file signature "magic bytes")
    private static readonly Dictionary<string, (string ContentType, byte[]? Signature)> AllowedTypes = new()
    {
        [".pdf"] = ("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 }),
        [".png"] = ("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        [".jpg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        [".jpeg"] = ("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF }),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                                        new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
        [".txt"] = ("text/plain", null)
    };

    private readonly AppDbContext _db;
    private readonly string _storageFolder;
    private readonly long _maxFileSize;

    public DocumentService(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _storageFolder = Path.GetFullPath(
            Path.Combine(env.ContentRootPath, config["Storage:Path"] ?? "Uploads"));
        _maxFileSize = config.GetValue<long>("Storage:MaxFileSizeBytes", 10 * 1024 * 1024);
    }

    public async Task<(DocumentResponse? Result, int Status, string? Error)> UploadAsync(IFormFile file)
    {
        // ---- Validation ----
        if (file == null || file.Length == 0)
            return (null, 400, "No file provided or the file is empty.");

        if (file.Length > _maxFileSize)
            return (null, 413, $"File exceeds the maximum size of {_maxFileSize / (1024 * 1024)} MB.");

        // Never trust the client's filename: strip any path parts
        var originalName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(originalName) || originalName.Length > 255)
            return (null, 400, "Invalid file name.");

        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(ext, out var type))
            return (null, 415, $"File type '{ext}' is not supported. Allowed: {string.Join(", ", AllowedTypes.Keys)}");

        if (!await HasValidSignatureAsync(file, type.Signature))
            return (null, 400, "File content does not match its extension.");

        // ---- Store file (server-generated name, so no collisions or path tricks) ----
        var id = Guid.NewGuid();
        var storedName = $"{id}{ext}";
        Directory.CreateDirectory(_storageFolder);
        var fullPath = Path.Combine(_storageFolder, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew))
            await file.CopyToAsync(stream);

        // ---- Save metadata; if the DB fails, remove the file so nothing is orphaned ----
        var doc = new Documents
        {
            Id = id,
            OriginalFileName = originalName,
            StoredFileName = storedName,
            ContentType = type.ContentType,   // server-determined, not the client's header
            FileSize = file.Length,
            DateandTime = DateTime.UtcNow
        };

        try
        {
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();
        }
        catch
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
            throw;
        }

        return (ToResponse(doc), 201, null);
    }

    public async Task<List<DocumentResponse>> GetAllAsync() =>
        await _db.Documents.AsNoTracking()
            .OrderByDescending(d => d.DateandTime)
            .Select(d => new DocumentResponse(d.Id, d.OriginalFileName, d.StoredFileName,
                                              d.ContentType, d.FileSize, d.DateandTime))
            .ToListAsync();

    public async Task<DocumentResponse?> GetByIdAsync(Guid id)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        return doc is null ? null : ToResponse(doc);
    }

    public async Task<(Documents? Doc, string? FullPath)> GetDownloadInfoAsync(Guid id)
    {
        var doc = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return (null, null);

        var path = ResolveSafePath(doc.StoredFileName);
        return (doc, path);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return false;

        var path = ResolveSafePath(doc.StoredFileName);

        // Delete the DB row and the file as one unit: if deleting the file throws,
        // the transaction rolls back and the record stays, so DB and disk stay consistent.
        await using var tx = await _db.Database.BeginTransactionAsync();
        _db.Documents.Remove(doc);
        await _db.SaveChangesAsync();

        if (path != null && File.Exists(path))
            File.Delete(path);

        await tx.CommitAsync();
        return true;
    }

    // ---------- helpers ----------

    // Returns the full path only if it stays inside the storage folder
    private string? ResolveSafePath(string storedName)
    {
        var full = Path.GetFullPath(Path.Combine(_storageFolder, storedName));
        return full.StartsWith(_storageFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? full : null;
    }

    private static async Task<bool> HasValidSignatureAsync(IFormFile file, byte[]? signature)
    {
        if (signature is null) return true;
        var header = new byte[signature.Length];
        await using var s = file.OpenReadStream();
        var read = await s.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        return read == signature.Length && header.SequenceEqual(signature);
    }

    private static DocumentResponse ToResponse(Documents d) =>
        new(d.Id, d.OriginalFileName, d.StoredFileName, d.ContentType, d.FileSize, d.DateandTime);
}