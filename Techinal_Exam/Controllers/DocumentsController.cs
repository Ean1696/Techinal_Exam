using Microsoft.AspNetCore.Mvc;
using Techinal_Exam.Services;

namespace Techinal_Exam.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _service;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(IDocumentService service, ILogger<DocumentsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // 4.1 Upload
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)] // 10 MB file + multipart overhead; service enforces exactly 10 MB
    public async Task<IActionResult> Upload(IFormFile file)
    {
        file ??= Request.Form.Files.FirstOrDefault();

        try
        {
            var (result, status, error) = await _service.UploadAsync(file);
            if (error != null) return StatusCode(status, new { error });
            return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed");
            return StatusCode(500, new { error = "The file could not be saved. Please try again." });
        }
    }

    // 4.2 List
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    // 4.3 Details
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var doc = await _service.GetByIdAsync(id);
        return doc is null ? NotFound(new { error = "Document not found." }) : Ok(doc);
    }

    // 4.4 Download
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id)
    {
        var (doc, path) = await _service.GetDownloadInfoAsync(id);

        if (doc is null)
            return NotFound(new { error = "Document not found." });

        if (path is null || !System.IO.File.Exists(path))
            return NotFound(new { error = "The document record exists but its file is missing from storage." });

        return PhysicalFile(path, doc.ContentType, doc.OriginalFileName);
    }

    // 4.5 Delete
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _service.DeleteAsync(id);
            return deleted ? NoContent() : NotFound(new { error = "Document not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for {Id}", id);
            return StatusCode(500, new { error = "The document could not be deleted. Please try again." });
        }
    }
}