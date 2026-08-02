using Microsoft.Extensions.Options;
using WebClient.Services.Admin;
using WebClient.Services.Auth;
using WebClient.Services.CvJd;
using WebClient.Services.Dashboard;
using WebClient.Services.Evaluation;
using WebClient.Services.Feedback;
using WebClient.Services.Home;
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
        services.AddMemoryCache(); // BlockedAccountMiddleware cache trạng thái account 30s
        services.AddTransient<JwtDelegatingHandler>();

        services.AddHttpClient<IApiClient, ApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;

            if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromMinutes(6);
        })
        .AddHttpMessageHandler<JwtDelegatingHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IBiometricsService, BiometricsService>();
        services.AddScoped<ICvJdService, CvJdService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IEvaluationService, EvaluationService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IManagerService, ManagerService>();
        services.AddScoped<IManagementDashboardService, ManagementDashboardService>();
        services.AddScoped<IManagerQuestionService, ManagerQuestionService>();
        services.AddScoped<IManagerTechStackService, ManagerTechStackService>();
        services.AddScoped<IManagerUserService, ManagerUserService>();
        services.AddScoped<IManagerNewsService, ManagerNewsService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<IAdminNewsService, AdminNewsService>();
        services.AddScoped<IAdminQuestionService, AdminQuestionService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IPaymentAdminService, PaymentAdminService>();
        services.AddScoped<IAdminTechStackService, AdminTechStackService>();
        return services;
    }
}