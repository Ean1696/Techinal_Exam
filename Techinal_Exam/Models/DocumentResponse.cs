namespace Techinal_Exam.Models
{
    public record DocumentResponse(
        Guid Id,
        string OriginalFileName,
        string StoredFileName,
        string ContentType,
        long FileSize,
        DateTime UploadedAt);
}