using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WebClient.Models.News;
using WebClient.Services.News;

namespace WebClient.Controllers.News;

[Route("Admin/News")]
public class AdminNewsController : Controller
{
    private readonly IAdminNewsService _newsService;
    private readonly IWebHostEnvironment _env;

    public AdminNewsController(
        IAdminNewsService newsService,
        IWebHostEnvironment env)
    {
        _newsService = newsService;
        _env = env;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (IsAdmin(context))
        {
            base.OnActionExecuting(context);
            return;
        }

        var request = context.HttpContext.Request;

        var isAjax =
            string.Equals(
                request.Headers["X-Requested-With"].ToString(),
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase
            );

        if (isAjax || request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false,
                message = "Bạn không có quyền thực hiện thao tác này."
            });

            return;
        }

        context.Result = Redirect("/Auth/Login");
    }

    private static bool IsAdmin(ActionExecutingContext context)
    {
        var request = context.HttpContext.Request;

        var role = request.Cookies["user_role"];
        var accessToken = request.Cookies["access_token"];
        var jwtToken = request.Cookies["JWToken"];

        var hasToken =
            !string.IsNullOrWhiteSpace(accessToken) ||
            !string.IsNullOrWhiteSpace(jwtToken);

        return hasToken &&
               string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] AdminNewsQueryDto query)
    {
        if (query.PageIndex <= 0)
            query.PageIndex = 1;

        if (query.PageSize <= 0)
            query.PageSize = 10;

        var viewModel = new AdminNewsViewModel
        {
            CurrentKeyword = query.Keyword,
            CurrentStatus = query.Status,
            CurrentCategoryId = query.CategoryId,
            CurrentPageIndex = query.PageIndex,
            CurrentPageSize = query.PageSize
        };

        ViewBag.CurrentQuery = query;
        ViewBag.CurrentCategory = query.CategoryId?.ToString() ?? "";
        ViewBag.CurrentStatus = query.Status ?? "";

        var newsResponse = await _newsService.GetNewsAsync(query);

        if (newsResponse != null && newsResponse.Success && newsResponse.Data != null)
        {
            viewModel.PagedNews = newsResponse.Data;
        }
        else
        {
            ViewBag.ErrorMessage = newsResponse?.Message ?? "Unable to load news articles.";
        }

        var categoryResponse = await _newsService.GetCategoriesAsync();

        if (categoryResponse != null && categoryResponse.Success && categoryResponse.Data != null)
        {
            viewModel.Categories = categoryResponse.Data;
        }
        else
        {
            ViewBag.CategoryError = categoryResponse?.Message ?? "Unable to load news categories.";
        }

        return View("~/Views/News/AdminNewsList.cshtml", viewModel);
    }

    [HttpGet("detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var response = await _newsService.GetNewsDetailAsync(id);

        if (response != null && response.Success && response.Data != null)
        {
            return View("~/Views/News/AdminNewsDetail.cshtml", response.Data);
        }

        TempData["ErrorMessage"] = response?.Message ?? "The requested article was not found.";
        return Redirect("/Admin/News");
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var model = new AdminNewsFormViewModel();
        await LoadCategoriesAsync(model);

        return View("~/Views/News/AdminNewsCreate.cshtml", model);
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromForm] AdminNewsFormViewModel model)
    {
        // BỎ điều kiện bắt buộc Slug — chỉ còn Title và Content
        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Content)||
                 string.IsNullOrWhiteSpace(model.Summary))
        {
            ViewBag.ErrorMessage = "Please fill in Title Content and Summary";
            await LoadCategoriesAsync(model);

            return View("~/Views/News/AdminNewsCreate.cshtml", model);
        }

        try
        {
            var uploadedUrl = await SaveThumbnailAsync(model.ThumbnailFile);
            var finalThumbnailUrl = uploadedUrl ?? model.ThumbnailUrl;

            // Nếu Slug admin để trống/toàn khoảng trắng -> gửi null lên API
            // để backend tự sinh slug từ Title (không gửi chuỗi rỗng "").
            var finalSlug = string.IsNullOrWhiteSpace(model.Slug)
                ? null
                : model.Slug.Trim();

            var dto = new AdminCreateNewsDto
            {
                CategoryId = model.CategoryId,
                Title = model.Title.Trim(),
                Slug = finalSlug,
                Summary = model.Summary,
                Content = model.Content,
                ThumbnailUrl = finalThumbnailUrl,
                Status = model.Status
            };

            var result = await _newsService.CreateNewsAsync(dto);

            if (result != null && result.Success && result.Data != null)
            {
                return Redirect($"/Admin/News/detail/{result.Data.NewsId}");
            }

            ViewBag.ErrorMessage = result?.Message ?? "Unable to create the news article.";
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = ex.Message;
        }

        await LoadCategoriesAsync(model);
        return View("~/Views/News/AdminNewsCreate.cshtml", model);
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var response = await _newsService.GetNewsDetailAsync(id);

        if (response == null || !response.Success || response.Data == null)
        {
            TempData["ErrorMessage"] = response?.Message ?? "The article to edit was not found.";
            return Redirect("/Admin/News");
        }

        var detail = response.Data;

        var model = new AdminNewsFormViewModel
        {
            NewsId = detail.NewsId,
            CategoryId = detail.CategoryId,
            Title = detail.Title,
            Slug = detail.Slug,
            Summary = detail.Summary,
            Content = detail.Content,
            ThumbnailUrl = detail.ThumbnailUrl,
            ExistingThumbnailUrl = detail.ThumbnailUrl,
            Status = detail.Status
        };

        await LoadCategoriesAsync(model);

        return View("~/Views/News/AdminNewsUpdate.cshtml", model);
    }

    [HttpPost("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromForm] AdminNewsFormViewModel model)
    {
        // BỎ điều kiện bắt buộc Slug — chỉ còn Title và Content
        if (string.IsNullOrWhiteSpace(model.Title) ||
            string.IsNullOrWhiteSpace(model.Content))
        {
            ViewBag.ErrorMessage = "Please fill in Title and Content.";
            model.NewsId = id;
            await LoadCategoriesAsync(model);

            return View("~/Views/News/AdminNewsUpdate.cshtml", model);
        }

        try
        {
            var uploadedUrl = await SaveThumbnailAsync(model.ThumbnailFile);

            var finalThumbnailUrl =
                uploadedUrl ??
                model.ThumbnailUrl ??
                model.ExistingThumbnailUrl;

            // Nếu Slug admin để trống/toàn khoảng trắng -> gửi null lên API
            // để backend tự sinh slug từ Title.
            var finalSlug = string.IsNullOrWhiteSpace(model.Slug)
                ? null
                : model.Slug.Trim();

            var dto = new AdminUpdateNewsDto
            {
                CategoryId = model.CategoryId,
                Title = model.Title.Trim(),
                Slug = finalSlug,
                Summary = model.Summary,
                Content = model.Content,
                ThumbnailUrl = finalThumbnailUrl,
                Status = model.Status
            };

            var result = await _newsService.UpdateNewsAsync(id, dto);

            if (result != null && result.Success && result.Data != null)
            {
                return Redirect($"/Admin/News/detail/{result.Data.NewsId}");
            }

            ViewBag.ErrorMessage = result?.Message ?? "Unable to update the news article.";
        }
        catch (Exception ex)
        {
            ViewBag.ErrorMessage = ex.Message;
        }

        model.NewsId = id;
        await LoadCategoriesAsync(model);

        return View("~/Views/News/AdminNewsUpdate.cshtml", model);
    }

    [HttpPost("delete/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _newsService.DeleteNewsAsync(id);

        if (result != null && result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "News article deleted successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result?.Message ?? "Unable to delete the news article."
        });
    }

    [HttpPost("restore/{id:guid}")]
    public async Task<IActionResult> Restore(Guid id)
    {
        var result = await _newsService.RestoreNewsAsync(id);

        if (result != null && result.Success)
        {
            return Ok(new
            {
                success = true,
                message = result.Message ?? "News article restored successfully."
            });
        }

        return BadRequest(new
        {
            success = false,
            message = result?.Message ?? "Unable to restore the news article."
        });
    }

    private async Task LoadCategoriesAsync(AdminNewsFormViewModel model)
    {
        var categoryResponse = await _newsService.GetCategoriesAsync();

        if (categoryResponse != null && categoryResponse.Success && categoryResponse.Data != null)
        {
            model.Categories = categoryResponse.Data;
        }
    }

    private async Task<string?> SaveThumbnailAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return null;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            throw new InvalidOperationException("Only .jpg, .jpeg, .png, .webp, or .gif files are allowed.");

        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("The maximum image size is 5MB.");

        var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "news");

        if (!Directory.Exists(uploadFolder))
        {
            Directory.CreateDirectory(uploadFolder);
        }

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return $"/uploads/news/{fileName}";
    }
}