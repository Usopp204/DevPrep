using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Feedback;
using WebClient.Services.Feedback;
using WebClient.Services.Interview;

namespace WebClient.Controllers.Feedback;

public class FeedbackController(IFeedbackService feedbackService, IInterviewService interviewService) : Controller
{
    private const int PublicPageSize = 5;

    public IActionResult Index() => RedirectToAction(nameof(System));

    [HttpGet]
    public async Task<IActionResult> System(int page = 1, CancellationToken cancellationToken = default)
    {
        var model = await BuildSystemModelAsync(page, PublicPageSize, cancellationToken);
        return View("Index", model);
    }

    [HttpGet]
    public async Task<IActionResult> Ai(int page = 1, CancellationToken cancellationToken = default)
    {
        var model = await BuildAiModelAsync(page, PublicPageSize, cancellationToken);
        return View("Index", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAi(
        Guid feedbackId,
        UpdateInterviewFeedbackRequest request,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticatedUser())
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = AiReturnUrl(page) });
        }

        request.Comment = FeedbackInput.NormalizeOptionalComment(request.Comment);
        if (!ModelState.IsValid)
        {
            return await RedisplayAiEditAsync(
                feedbackId,
                request,
                page,
                CommentValidationMessage() ?? "Please choose a 1-5 rating for each required category.",
                cancellationToken);
        }

        var result = await feedbackService.EditInterviewFeedbackAsync(feedbackId, request, cancellationToken);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Your AI feedback has been updated successfully.";
        }
        else if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = AiReturnUrl(page) });
        }
        else if (result.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity)
        {
            return await RedisplayAiEditAsync(
                feedbackId,
                request,
                page,
                result.Error ?? "Please correct the feedback fields and try again.",
                cancellationToken);
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not update AI feedback.";
        }

        return RedirectToAction(nameof(Ai), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAi(
        Guid feedbackId,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticatedUser())
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = AiReturnUrl(page) });
        }

        var result = await feedbackService.DeleteInterviewFeedbackAsync(feedbackId, cancellationToken);
        if (result.Success || result.StatusCode == StatusCodes.Status404NotFound)
        {
            TempData["SuccessMessage"] = "Your AI feedback has been deleted.";
        }
        else if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = AiReturnUrl(page) });
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not delete AI feedback.";
        }

        return RedirectToAction(nameof(Ai), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitSystem(
        SubmitSystemFeedbackRequest request,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!IsAuthenticatedUser())
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }

        request.Comment = FeedbackInput.NormalizeOptionalComment(request.Comment);
        if (!ModelState.IsValid)
        {
            var validationMessage = CommentValidationMessage()
                ?? "Please choose a 1-5 rating for each required category.";
            return await RedisplayOrRedirectSystemAsync(request, returnUrl, validationMessage, cancellationToken);
        }

        var result = await feedbackService.SubmitSystemFeedbackAsync(request, cancellationToken);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Your system feedback has been published. Thank you for helping us improve DevPrep.";
            return RedirectSafely(returnUrl);
        }

        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }

        var message = ToFeedbackMessage(result.StatusCode, result.Error, "Could not submit system feedback.");
        var fieldMessage = result.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity
            && result.Error?.Contains("Comment", StringComparison.OrdinalIgnoreCase) == true
                ? result.Error
                : null;
        return await RedisplayOrRedirectSystemAsync(request, returnUrl, message, cancellationToken, fieldMessage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSystem(
        Guid feedbackId,
        UpdateSystemFeedbackRequest request,
        int page = 1,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticatedUser())
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }

        request.Comment = FeedbackInput.NormalizeOptionalComment(request.Comment);
        if (!ModelState.IsValid)
        {
            var model = await BuildSystemModelAsync(page, PublicPageSize, cancellationToken);
            model.EditSystemForm = request;
            model.EditingFeedbackId = feedbackId;
            model.EditFormWasSubmitted = true;
            model.FeedbackValidationMessage = CommentValidationMessage();
            model.ErrorMessage = model.FeedbackValidationMessage
                ?? "Please choose a 1-5 rating for each required category.";
            return View("Index", model);
        }

        var result = await feedbackService.EditSystemFeedbackAsync(feedbackId, request, cancellationToken);
        if (result.Success)
        {
            TempData["SuccessMessage"] = "Your system feedback has been updated successfully.";
        }
        else if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }
        else if (result.StatusCode is StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity)
        {
            var model = await BuildSystemModelAsync(page, PublicPageSize, cancellationToken);
            model.EditSystemForm = request;
            model.EditingFeedbackId = feedbackId;
            model.EditFormWasSubmitted = true;
            model.FeedbackValidationMessage = result.Error?.Contains("Comment", StringComparison.OrdinalIgnoreCase) == true
                ? result.Error
                : null;
            model.ErrorMessage = result.Error ?? "Please correct the feedback fields and try again.";
            return View("Index", model);
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not update system feedback.";
        }

        return RedirectToAction(nameof(System), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSystem(
        Guid feedbackId,
        int page = 1,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticatedUser())
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }

        var result = await feedbackService.DeleteSystemFeedbackAsync(feedbackId, cancellationToken);
        if (result.Success || result.StatusCode == StatusCodes.Status404NotFound)
        {
            TempData["SuccessMessage"] = "Your system feedback has been deleted.";
        }
        else if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return RedirectToAction("Login", "Auth", new { returnUrl = LocalReturnUrl(returnUrl) });
        }
        else
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not delete system feedback.";
        }

        return RedirectToAction(nameof(System), new { page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Consumes("application/json")]
    public async Task<IActionResult> SubmitReport(
        [FromQuery] Guid feedbackId,
        [FromQuery] string feedbackType,
        [FromQuery] string? returnUrl,
        [FromBody] CreateFeedbackReportRequest? request,
        CancellationToken cancellationToken)
    {
        var safeReturnUrl = LocalReturnUrl(returnUrl);
        if (!IsAuthenticatedUser())
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                success = false,
                message = "Please log in to report feedback.",
                loginUrl = Url.Action("Login", "Auth", new { returnUrl = safeReturnUrl })
            });
        }

        request ??= new CreateFeedbackReportRequest();
        request.Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details;
        if (feedbackId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(feedbackId), "The feedback is no longer available.");
        }

        if (feedbackType != FeedbackReportRules.SystemType)
        {
            var err = feedbackType == FeedbackReportRules.InterviewType
                ? "AI feedback cannot be reported. Only system feedback can be reported."
                : "Feedback type is invalid.";
            ModelState.AddModelError(nameof(feedbackType), err);
        }

        if (string.IsNullOrWhiteSpace(request.ReasonCode) || !FeedbackReportRules.Reasons.ContainsKey(request.ReasonCode))
        {
            ModelState.AddModelError(nameof(request.ReasonCode), "Choose a valid report reason.");
        }

        if (request.ReasonCode == FeedbackReportRules.OtherReason && request.Details is null)
        {
            ModelState.AddModelError(nameof(request.Details), "Details are required when the reason is Other.");
        }

        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "The supplied value is invalid."
                            : error.ErrorMessage)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var message = errors.Values.SelectMany(value => value)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
                ?? "Please correct the report fields and try again.";
            return BadRequest(new { success = false, message, errors });
        }

        var result = await feedbackService.SubmitFeedbackReportAsync(
            feedbackType,
            feedbackId,
            request,
            cancellationToken);
        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                data = result.Data,
                message = result.Data?.Message ?? "Thank you. Your report has been submitted for review."
            });
        }

        if (result.StatusCode == StatusCodes.Status401Unauthorized)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                success = false,
                message = "Please log in to report feedback.",
                loginUrl = Url.Action("Login", "Auth", new { returnUrl = safeReturnUrl })
            });
        }

        return StatusCode(result.StatusCode, new
        {
            success = false,
            message = ToReportMessage(result.StatusCode, result.Error)
        });
    }

    private async Task<IActionResult> RedisplayOrRedirectSystemAsync(
        SubmitSystemFeedbackRequest request,
        string? returnUrl,
        string message,
        CancellationToken cancellationToken,
        string? fieldMessage = null)
    {
        if (IsSystemFeedbackPage(returnUrl))
        {
            var model = await BuildSystemModelAsync(1, PublicPageSize, cancellationToken);
            model.SystemForm = request;
            model.FeedbackFormWasSubmitted = true;
            model.FeedbackValidationMessage = fieldMessage ?? CommentValidationMessage();
            model.ErrorMessage = message;
            return View("Index", model);
        }

        TempData[FeedbackTempDataKeys.SystemForm] = JsonSerializer.Serialize(request);
        TempData[FeedbackTempDataKeys.SystemValidation] = fieldMessage ?? CommentValidationMessage();
        TempData["ErrorMessage"] = message;
        return RedirectSafely(returnUrl);
    }

    private async Task<IActionResult> RedisplayAiEditAsync(
        Guid feedbackId,
        UpdateInterviewFeedbackRequest request,
        int page,
        string message,
        CancellationToken cancellationToken)
    {
        var model = await BuildAiModelAsync(page, PublicPageSize, cancellationToken);
        model.EditInterviewForm = request;
        model.EditingFeedbackId = feedbackId;
        model.EditFormWasSubmitted = true;
        model.FeedbackValidationMessage = CommentValidationMessage();
        model.ErrorMessage = message;
        return View("Index", model);
    }

    private async Task<FeedbackPageViewModel> BuildAiModelAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var reviews = await feedbackService.GetAiReviewsAsync(page, pageSize, cancellationToken);
        if (reviews.Success && reviews.Data is not null && reviews.Data.Items.Count == 0 && page > 1)
        {
            page = Math.Max(1, reviews.Data.TotalPages);
            reviews = await feedbackService.GetAiReviewsAsync(page, pageSize, cancellationToken);
        }

        return new FeedbackPageViewModel
        {
            ActiveSection = "Ai",
            AiReviews = reviews.Data ?? new WebClient.Models.Shared.PagedResult<PublicAiFeedbackDto>
            {
                Page = Math.Max(1, page),
                PageSize = pageSize
            },
            Page = reviews.Data?.Page ?? Math.Max(1, page),
            PageSize = pageSize,
            IsAuthenticatedUser = IsAuthenticatedUser(),
            ErrorMessage = reviews.Success ? TempData["ErrorMessage"] as string : reviews.Error,
            SuccessMessage = TempData["SuccessMessage"] as string
        };
    }

    private async Task<FeedbackPageViewModel> BuildSystemModelAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var reviews = await feedbackService.GetSystemReviewsAsync(page, pageSize, cancellationToken);
        if (reviews.Success && reviews.Data is not null && reviews.Data.Items.Count == 0 && page > 1)
        {
            page = Math.Max(1, reviews.Data.TotalPages);
            reviews = await feedbackService.GetSystemReviewsAsync(page, pageSize, cancellationToken);
        }

        var model = new FeedbackPageViewModel
        {
            ActiveSection = "System",
            SystemReviews = reviews.Data ?? new WebClient.Models.Shared.PagedResult<PublicSystemFeedbackDto> { Page = Math.Max(1, page), PageSize = pageSize },
            Page = reviews.Data?.Page ?? Math.Max(1, page),
            PageSize = pageSize,
            IsAuthenticatedUser = IsAuthenticatedUser(),
            ErrorMessage = reviews.Success ? TempData["ErrorMessage"] as string : reviews.Error,
            SuccessMessage = TempData["SuccessMessage"] as string
        };

        if (model.IsAuthenticatedUser)
        {
            var status = await feedbackService.GetSystemSubmissionStatusAsync(cancellationToken);
            if (status.Success && status.Data?.ActiveFeedbackId is Guid feedbackId)
            {
                var owned = await feedbackService.GetMySystemFeedbackAsync(feedbackId, cancellationToken);
                if (owned.Success && owned.Data is not null)
                {
                    model.MySystemFeedback = owned.Data;
                    if (model.SystemReviews.Items.All(item => item.FeedbackId != owned.Data.FeedbackId))
                    {
                        model.SystemReviews.Items.Insert(0, new PublicSystemFeedbackDto
                        {
                            FeedbackId = owned.Data.FeedbackId,
                            AuthorDisplayName = "You",
                            OverallRating = owned.Data.OverallRating,
                            UiUxRating = owned.Data.UiUxRating,
                            UsabilityRating = owned.Data.UsabilityRating,
                            FeatureQualityRating = owned.Data.FeatureQualityRating,
                            Comment = owned.Data.Comment,
                            CreatedAt = owned.Data.CreatedAt,
                            IsOwner = true
                        });
                    }
                }
                else
                {
                    model.ErrorMessage ??= "Could not load your active system feedback right now.";
                }
            }
            else if (!status.Success && status.StatusCode != StatusCodes.Status404NotFound)
            {
                model.ErrorMessage ??= "Could not confirm your system feedback status right now.";
            }

            var eligibility = await interviewService.GetFeedbackEligibilityAsync(cancellationToken);
            if (eligibility.Success)
            {
                model.Eligibility = eligibility.Data;
            }
            else
            {
                model.ErrorMessage ??= "Could not confirm feedback eligibility right now.";
            }
        }

        return model;
    }

    private string? CommentValidationMessage()
    {
        if (!ModelState.TryGetValue(nameof(SubmitSystemFeedbackRequest.Comment), out var entry))
        {
            return null;
        }

        return entry.Errors.Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
    }

    private bool IsAuthenticatedUser()
    {
        var token = Request.Cookies["access_token"];
        var role = Request.Cookies["user_role"];
        return !string.IsNullOrEmpty(token) && string.Equals(role, "User", StringComparison.OrdinalIgnoreCase);
    }

    private IActionResult RedirectSafely(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(System));
    }

    private string AiReturnUrl(int page) =>
        Url.Action(nameof(Ai), "Feedback", new { page }) ?? "/Feedback/Ai";

    private string LocalReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action(nameof(System), "Feedback") ?? "/Feedback/System";

    private bool IsSystemFeedbackPage(string? returnUrl) =>
        string.IsNullOrWhiteSpace(returnUrl)
        || (Url.IsLocalUrl(returnUrl)
            && returnUrl.StartsWith("/Feedback", StringComparison.OrdinalIgnoreCase));

    private static string ToReportMessage(int statusCode, string? error) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => error ?? "Please correct the report fields and try again.",
            StatusCodes.Status403Forbidden => "You do not have permission to report this feedback.",
            StatusCodes.Status404NotFound => "This feedback is no longer publicly available.",
            StatusCodes.Status409Conflict => "You have already reported this feedback.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please wait and try again.",
            StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout => "Reporting is temporarily unavailable. Your selections have been kept; please try again.",
            _ => "The report could not be submitted. Please try again."
        };

    private static string ToFeedbackMessage(int statusCode, string? error, string fallback) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => error ?? "Please correct the feedback fields and try again.",
            StatusCodes.Status403Forbidden => "You do not have permission to submit system feedback.",
            StatusCodes.Status404NotFound => "The feedback session is no longer available.",
            StatusCodes.Status409Conflict => error ?? "You need at least one completed or expired interview before submitting system feedback.",
            StatusCodes.Status429TooManyRequests => "Too many requests. Please wait and try again.",
            StatusCodes.Status500InternalServerError or StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout => "Feedback is temporarily unavailable. Please try again later.",
            _ => string.IsNullOrWhiteSpace(error) ? fallback : error
        };
}
