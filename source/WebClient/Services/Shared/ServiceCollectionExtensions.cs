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
    /// <summary>
    /// Named client for the CV/JD document routes — the only calls WebClient makes whose
    /// downstream budget exceeds a minute. Everything else uses the default <see cref="IApiClient"/>.
    /// </summary>
    public const string DocumentsHttpClient = "documents";

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

        // ── Timeout ladder — each layer MUST outlast the one it wraps ────────────────────
        //
        //   ai-service  60s  ->  ApiGateway  90s  ->  WebClient  95s   (default client)
        //   ai-service 300s  ->  ApiGateway 330s  ->  WebClient 335s   (documents client)
        //
        // The 5-second margins are load-bearing, not decoration. If WebClient's deadline
        // equalled the gateway's, the two would race: when WebClient wins, HttpClient throws
        // TaskCanceledException and the user sees raw framework text ("The request was
        // canceled due to the configured HttpClient.Timeout of ... elapsing"); when the
        // gateway wins, it returns a clean 504 that the service layer can turn into a
        // readable message. Giving the outer layer the longer deadline makes the gateway win
        // deterministically. Do NOT "tidy" these into matching round numbers.
        //
        // Why 95s is enough for everything except documents: ai-service is capped at 60s by
        // both InterviewAPI (Program.cs:96,118) and EvaluationAPI (Program.cs:62), and every
        // other downstream client in the solution is 15-30s. CvJdAPI's 300s (Program.cs:24)
        // is the single budget above a minute, which is why it gets its own client below.
        services.AddHttpClient<IApiClient, ApiClient>((provider, client) =>
            ConfigureGatewayClient(provider, client, TimeSpan.FromSeconds(95)))
        .AddHttpMessageHandler<JwtDelegatingHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseProxy = false });

        // 335s = CvJdAPI's 300s ai-service budget (CvJdAPI/Program.cs:24), plus the gateway's
        // document policy at 330s, plus one hop of margin. Same ladder rule as above: this
        // must stay strictly greater than the gateway's document timeout, or a slow CV parse
        // surfaces as a raw client-side cancellation instead of the gateway's 504.
        services.AddHttpClient(DocumentsHttpClient, (provider, client) =>
            ConfigureGatewayClient(provider, client, TimeSpan.FromSeconds(335)))
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
        services.AddScoped<IManagementDashboardSourceClient, ManagementDashboardSourceClient>();
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

    // Shared by both clients above so the base address and handler wiring cannot drift apart;
    // the timeout is the only thing that differs between them.
    private static void ConfigureGatewayClient(IServiceProvider provider, HttpClient client, TimeSpan timeout)
    {
        var options = provider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;

        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }

        client.Timeout = timeout;
    }
}