using WebClient.Models.Payment;
using WebClient.Services.Shared;

namespace WebClient.Services.Payment;

public interface IPaymentService
{
    PaymentViewModel GetOverview();
}

public sealed class PaymentService(IApiClient apiClient) : IPaymentService
{
    public PaymentViewModel GetOverview()
    {
        _ = apiClient;

        return new PaymentViewModel
        {
            ModuleName = "Payment & Service",
            Epic = "Payment & Service",
            Summary = "Payment and service placeholder for future API Gateway integration."
        };
    }
}
