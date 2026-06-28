using Microsoft.Extensions.Options;
using WebClient.Services.Admin;
using WebClient.Services.Auth;
using WebClient.Services.CvJd;
using WebClient.Services.Evaluation;
using WebClient.Services.Interview;
using WebClient.Services.Manager;
using WebClient.Services.News;
using WebClient.Services.Payment;
using WebClient.Services.Profile;

namespace WebClient.Services.Shared;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiGatewayOptions>(configuration.GetSection("ApiGateway"));

        services.AddHttpClient<IApiClient, ApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;

            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICvJdService, CvJdService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IManagerService, ManagerService>();
        services.AddScoped<INewsService, NewsService>();

        return services;
    }
}
