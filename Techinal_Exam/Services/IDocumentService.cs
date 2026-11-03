using Techinal_Exam.Models;

namespace Techinal_Exam.Services;

public interface IDocumentService
{
    Task<(DocumentResponse? Result, int Status, string? Error)> UploadAsync(IFormFile file);
    Task<List<DocumentResponse>> GetAllAsync();
    Task<DocumentResponse?> GetByIdAsync(Guid id);
    Task<(Documents? Doc, string? FullPath)> GetDownloadInfoAsync(Guid id);
    Task<bool> DeleteAsync(Guid id);
}