using WebClient.Services.Home;
using WebClient.Services.Shared;

var builder = WebApplication.CreateBuilder(args);

// Explicit UseUrls(), not ASPNETCORE_URLS: appsettings.json used to carry its own
// "Urls" key, which occupies the same configuration key ("urls") that ASPNETCORE_URLS
// would need to override — but the unprefixed environment-variable provider that
// builds this IConfiguration reads that env var under the literal key
// "ASPNETCORE_URLS", never touching "urls", so appsettings.json's value always won
// regardless of what ASPNETCORE_URLS was set to (see phase1-checklist.md H-2). The
// stale "Urls" key has been removed from appsettings.json; this explicit call is now
// the single source of truth, matching the pattern every backend service already
// uses (SERVICE_BIND_HOST + <SVC>_API_PORT).
var servicePort = builder.Configuration.GetValue<int>("WEBCLIENT_PORT", 5130);
var bindHost = builder.Configuration.GetValue<string>("SERVICE_BIND_HOST", "localhost");
builder.WebHost.UseUrls($"http://{bindHost}:{servicePort}");

builder.Services.AddControllersWithViews();
builder.Services.AddWebClientServices(builder.Configuration);
builder.Services.AddSingleton<IHomeFeedbackPopupGate, HomeFeedbackPopupGate>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".DevPrep.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

// Liveness probe for the container HEALTHCHECK. Deliberately trivial — a literal 200, no
// dependency checks.
// Registered as the FIRST middleware, before UseHttpsRedirection and before the auth/account
// middlewares, for two reasons: the container serves plain HTTP, so a redirect to HTTPS would
// make the probe meaningless; and the probe must not be affected by session, authentication,
// or the blocked-account checks further down the pipeline.
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health")
    {
        await context.Response.WriteAsJsonAsync(new { service = "WebClient", status = "ok" });
        return;
    }

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<WebClient.Middlewares.AccountValidationMiddleware>();

// Tài khoản bị block giữa phiên -> tự đăng xuất + đá về Login (check status qua AuthAPI, cache 30s).
app.UseMiddleware<WebClient.Middlewares.BlockedAccountMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
