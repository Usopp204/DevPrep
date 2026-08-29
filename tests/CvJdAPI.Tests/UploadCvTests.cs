using System.Net;
using CvJdAPI.Endpoints;
using CvJdAPI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CvJdAPI.Tests;

public class UploadCvTests
{
    [Fact(DisplayName = "FUNC021-USR-UTCID01")]
    public async Task Valid_pdf_is_saved_parsed_and_made_current()
    {
        var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); var user = Guid.NewGuid(); var old = new Cv { CvId = Guid.NewGuid(), UserId = user, IsCurrent = true, UploadedAt = DateTime.UtcNow.AddDays(-1) }; db.Cvs.Add(old); await db.SaveChangesAsync();
            var result = await CvEndpoints.UploadCvAsync(File("candidate.pdf", 2048), user.ToString(), db, Factory(HttpStatusCode.OK, "{\"skills\":[\"C#\"]}"), Config(dir)); Assert.Equal(200, ViewCurrentCvTests.Status(result)); var current = Assert.Single(db.Cvs.Where(x => x.IsCurrent)); Assert.NotSame(old, current); Assert.False(old.IsCurrent); Assert.Equal("Parsed", current.ParseStatus); Assert.Equal(2, current.FileSizeKb); Assert.True(System.IO.File.Exists(Path.Combine(dir, Path.GetFileName(current.FileUrl)))); }
        finally { Directory.Delete(dir, true); }
    }
    [Fact(DisplayName = "FUNC021-USR-UTCID02")] public async Task Invalid_identifier_format_is_rejected() { var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(File("cv.pdf"), "invalid-format", db, Factory(), Config(dir)))); Assert.Empty(db.Cvs); Assert.Empty(Directory.GetFiles(dir)); } finally { Directory.Delete(dir, true); } }
    [Fact(DisplayName = "FUNC021-USR-UTCID03")] public async Task Missing_file_or_identifier_is_rejected() { var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(null, Guid.NewGuid().ToString(), db, Factory(), Config(dir)))); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(File("cv.pdf"), null, db, Factory(), Config(dir)))); } finally { Directory.Delete(dir, true); } }
    [Fact(DisplayName = "FUNC021-USR-UTCID04")] public async Task Unsupported_file_extension_is_rejected() { var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(400, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(File("malware.exe"), Guid.NewGuid().ToString(), db, Factory(), Config(dir)))); Assert.Empty(db.Cvs); Assert.Empty(Directory.GetFiles(dir)); } finally { Directory.Delete(dir, true); } }
    [Fact(DisplayName = "FUNC021-USR-UTCID05")] public async Task Boundary_identifiers_are_accepted() { var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); Assert.Equal(200, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(File("min.pdf"), Guid.Empty.ToString(), db, Factory(), Config(dir)))); Assert.Equal(200, ViewCurrentCvTests.Status(await CvEndpoints.UploadCvAsync(File("max.docx"), "ffffffff-ffff-ffff-ffff-ffffffffffff", db, Factory(), Config(dir)))); Assert.Equal(2, db.Cvs.Count()); } finally { Directory.Delete(dir, true); } }
    [Fact(DisplayName = "FUNC021-USR-UTCID06")] public async Task Missing_ai_configuration_raises_dependency_error_without_db_record() { var dir = TempDir(); try { await using var db = ViewCurrentCvTests.CreateDb(); var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CV_UPLOADS_PATH"] = dir }).Build(); await Assert.ThrowsAsync<InvalidOperationException>(() => CvEndpoints.UploadCvAsync(File("cv.pdf"), Guid.NewGuid().ToString(), db, Factory(), config)); Assert.Empty(db.Cvs); } finally { Directory.Delete(dir, true); } }

    private static IFormFile File(string name, int bytes = 32) { var stream = new MemoryStream(new byte[bytes]); return new FormFile(stream, 0, bytes, "file", name) { Headers = new HeaderDictionary(), ContentType = "application/pdf" }; }
    private static string TempDir() { var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(path); return path; }
    private static IConfiguration Config(string dir) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CV_UPLOADS_PATH"] = dir, ["AI_SERVICE_URL"] = "http://ai" }).Build();
    private static IHttpClientFactory Factory(HttpStatusCode status = HttpStatusCode.OK, string body = "{}") => new FactoryStub(new HttpClient(new Handler(status, body)));
    private sealed class FactoryStub(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
}
