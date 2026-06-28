using Microsoft.AspNetCore.Mvc;
using WebClient.Services.Payment;

namespace WebClient.Controllers.Payment;

public class PaymentController(IPaymentService paymentService) : Controller
{
    public IActionResult Index()
    {
        return View(paymentService.GetOverview());
    }
}
