using Microsoft.AspNetCore.Mvc;
using WebClient.Services.CvJd;

namespace WebClient.Controllers.CvJd;

public class CvJdController(ICvJdService cvJdService) : Controller
{
    public IActionResult Index() => View(cvJdService.GetOverview());

    [HttpGet]
    public async Task<IActionResult> GetCvInfo()
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        var r = await cvJdService.GetCvInfoAsync(userId, token);
        return r is null ? NotFound() : Content(r, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> UploadCv(IFormFile file)
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file" });
        var r = await cvJdService.UploadCvAsync(userId, file, token);
        return Content(r ?? "{}", "application/json");
    }

    [HttpPut]
    public async Task<IActionResult> UpdateCvJson([FromBody] UpdateCvJsonRequest body)
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        var r = await cvJdService.UpdateCvJsonAsync(userId, body.ParsedJson, token);
        return Content(r ?? "{}", "application/json");
    }

    [HttpGet]
    public async Task<IActionResult> GetJdInfo()
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        var r = await cvJdService.GetJdInfoAsync(userId, token);
        return r is null ? NotFound() : Content(r, "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> SaveJd([FromForm] string jdText, [FromForm] string? position, [FromForm] string parseMode = "ai")
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(jdText)) return BadRequest(new { error = "JD text empty" });
        var r = await cvJdService.SaveJdAsync(userId, jdText, position, parseMode, token);
        return Content(r ?? "{}", "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> UploadJdFile(IFormFile file, [FromForm] string? position)
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file" });
        var r = await cvJdService.UploadJdFileAsync(userId, file, position, token);
        return Content(r ?? "{}", "application/json");
    }

    [HttpPost]
    public async Task<IActionResult> Analyze()
    {
        var (userId, token) = GetCtx();
        if (userId is null || token is null) return Unauthorized();
        var r = await cvJdService.AnalyzeAsync(userId, token);
        return Content(r ?? "{}", "application/json");
    }

    private (string? userId, string? token) GetCtx() =>
        (string.IsNullOrEmpty(Request.Cookies["user_id"]) ? null : Request.Cookies["user_id"],
         string.IsNullOrEmpty(Request.Cookies["access_token"]) ? null : Request.Cookies["access_token"]);
}

public record UpdateCvJsonRequest(string ParsedJson);
