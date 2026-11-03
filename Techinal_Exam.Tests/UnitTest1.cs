using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Techinal_Exam.Data;
using Techinal_Exam.Services;

namespace Techinal_Exam.Tests;

public class DocumentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly string _root;
    private readonly string _uploads;
    private readonly DocumentService _service;

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 test content");

    public DocumentServiceTests()
    {
        // Fake in-memory database (fresh for every test)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Temp folder for stored files, so tests never touch your real Uploads folder
        _root = Path.Combine(Path.GetTempPath(), "doc-tests-" + Guid.NewGuid());
        _uploads = Path.Combine(_root, "Uploads");
        Directory.CreateDirectory(_root);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Path"] = "Uploads",
            ["Storage:MaxFileSizeBytes"] = "1024"   // 1 KB limit keeps the oversize test tiny
        }).Build();

        _service = new DocumentService(_db, config, new FakeEnv(_root));
    }

    private static IFormFile MakeFile(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    // ---------- Required tests ----------

    [Fact]
    public async Task Upload_ValidPdf_StoresFileAndMetadata()
    {
        var (result, status, error) = await _service.UploadAsync(MakeFile("report.pdf", PdfBytes));

        Assert.Null(error);
        Assert.Equal(201, status);
        Assert.NotNull(result);
        Assert.Equal("report.pdf", result!.OriginalFileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal(PdfBytes.Length, result.FileSize);
        Assert.True(File.Exists(Path.Combine(_uploads, result.StoredFileName)));
        Assert.Equal(1, await _db.Documents.CountAsync());
    }

    [Fact]
    public async Task GetById_ExistingDocument_ReturnsIt()
    {
        var (uploaded, _, _) = await _service.UploadAsync(MakeFile("report.pdf", PdfBytes));

        var found = await _service.GetByIdAsync(uploaded!.Id);

        Assert.NotNull(found);
        Assert.Equal(uploaded.Id, found!.Id);
        Assert.Equal("report.pdf", found.OriginalFileName);
    }

    [Fact]
    public async Task GetById_NonexistentDocument_ReturnsNull()
    {
        var found = await _service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task Upload_ExceedingSizeLimit_Returns413AndStoresNothing()
    {
        var tooBig = new byte[2048];   // larger than the 1 KB test limit

        var (result, status, error) = await _service.UploadAsync(MakeFile("big.pdf", tooBig));

        Assert.Null(result);
        Assert.Equal(413, status);
        Assert.NotNull(error);
        Assert.Equal(0, await _db.Documents.CountAsync());
        Assert.False(Directory.Exists(_uploads) && Directory.GetFiles(_uploads).Length > 0);
    }

    [Fact]
    public async Task Delete_ExistingDocument_RemovesRecordAndFile()
    {
        var (uploaded, _, _) = await _service.UploadAsync(MakeFile("report.pdf", PdfBytes));
        var filePath = Path.Combine(_uploads, uploaded!.StoredFileName);

        var deleted = await _service.DeleteAsync(uploaded.Id);

        Assert.True(deleted);
        Assert.False(File.Exists(filePath));
        Assert.Equal(0, await _db.Documents.CountAsync());
    }

    // ---------- Extra tests ----------

    [Fact]
    public async Task Delete_NonexistentDocument_ReturnsFalse()
    {
        Assert.False(await _service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Upload_UnsupportedExtension_Returns415()
    {
        var (result, status, _) = await _service.UploadAsync(MakeFile("virus.exe", PdfBytes));

        Assert.Null(result);
        Assert.Equal(415, status);
    }

    [Fact]
    public async Task Upload_ContentDoesNotMatchExtension_Returns400()
    {
        var notAPdf = Encoding.ASCII.GetBytes("this is plain text, not a pdf");

        var (result, status, _) = await _service.UploadAsync(MakeFile("fake.pdf", notAPdf));

        Assert.Null(result);
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var (result, status, _) = await _service.UploadAsync(MakeFile("empty.pdf", Array.Empty<byte>()));

        Assert.Null(result);
        Assert.Equal(400, status);
    }

    [Fact]
    public async Task Upload_PathTraversalFilename_IsStoredUnderGuidInsideUploads()
    {
        var (result, status, _) = await _service.UploadAsync(MakeFile("../../evil.pdf", PdfBytes));

        Assert.Equal(201, status);
        Assert.Equal("evil.pdf", result!.OriginalFileName);              // path parts stripped
        Assert.DoesNotContain("evil", result.StoredFileName);            // stored under a GUID name
        Assert.True(File.Exists(Path.Combine(_uploads, result.StoredFileName)));
        Assert.False(File.Exists(Path.Combine(_root, "evil.pdf")));      // nothing escaped the folder
    }

    [Fact]
    public async Task Download_WhenPhysicalFileIsMissing_ReportsRecordButNoFile()
    {
        var (uploaded, _, _) = await _service.UploadAsync(MakeFile("report.pdf", PdfBytes));
        File.Delete(Path.Combine(_uploads, uploaded!.StoredFileName));

        var (doc, path) = await _service.GetDownloadInfoAsync(uploaded.Id);

        Assert.NotNull(doc);                  // record still exists
        Assert.False(File.Exists(path));      // but the file is gone
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}

// Minimal stand-in for the web host environment the service needs
internal class FakeEnv : IWebHostEnvironment
{
    public FakeEnv(string root) => ContentRootPath = root;

    public string ApplicationName { get; set; } = "Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}