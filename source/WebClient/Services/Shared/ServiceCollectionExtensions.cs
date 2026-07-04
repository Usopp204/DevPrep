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
        services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.AccessDeniedPath = "/Auth/Login";
                options.Cookie.Name = "WebClient_Auth";
            });

        services.Configure<ApiGatewayOptions>(configuration.GetSection("ApiGateway"));
        services.AddHttpContextAccessor();
        services.AddTransient<JwtDelegatingHandler>();

        // Cho ProfileService đọc cookie access_token để gắn Bearer khi gọi API.
        services.AddHttpContextAccessor();

        services.AddHttpClient<IApiClient, ApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;

            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }
        })
        .AddHttpMessageHandler<JwtDelegatingHandler>()
        // Gọi tới API Gateway ở localhost: KHÔNG đi qua proxy hệ thống (proxy có thể chặn localhost -> timeout).
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICvJdService, CvJdService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IManagerService, ManagerService>();
        services.AddScoped<IManagerQuestionService, ManagerQuestionService>();
        services.AddScoped<IManagerUserService, ManagerUserService>();
        services.AddScoped<IManagerNewsService, ManagerNewsService>();
        services.AddScoped<INewsService, NewsService>();

        return services;
    }
}
