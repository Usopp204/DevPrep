using Microsoft.AspNetCore.Mvc;
using WebClient.Models.Payment;
using WebClient.Services.Payment;
using WebClient.Services.Shared;

namespace WebClient.Controllers.Payment;

public class PaymentController(IPaymentService paymentService) : Controller
{
    private bool IsAdminOrManager()
    {
        var role = Request.Cookies["user_role"] ?? string.Empty;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        if (IsAdminOrManager())
            return RedirectToAction("Index", "Admin");

        var model = await paymentService.GetBuyCreditsAsync(ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromForm] Guid packageId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(Request.Cookies["access_token"]))
            return RedirectToAction("Login", "Auth");

        if (IsAdminOrManager())
            return RedirectToAction("Index", "Admin");

        var userId = JwtUserId.FromToken(Request.Cookies["access_token"]);
        if (userId is null)
        {
            TempData["PaymentError"] = "Phiên đăng nhập không hợp lệ. Vui lòng thử lại.";
            return RedirectToAction("Index");
        }

        try
        {
            var result = await paymentService.CreateOrderAsync(packageId, userId.Value, ct);
            return Redirect(result.PaymentUrl);
        }
        catch (Exception)
        {
            TempData["PaymentError"] = "Không thể tạo đơn thanh toán. Vui lòng thử lại.";
            return RedirectToAction("Index");
        }
    }

    public IActionResult Success(string? orderCode, int credits, string? amount)
    {
        return View(new PaymentSuccessViewModel
        {
            OrderCode = orderCode ?? "Unknown",
            Credits   = credits,
            Amount    = amount ?? "0"
        });
    }

    public IActionResult Failed(string? reason, string? orderCode)
    {
        return View(new PaymentFailedViewModel
        {
            Reason    = reason ?? "The payment was not completed.",
            OrderCode = orderCode
        });
    }
}
