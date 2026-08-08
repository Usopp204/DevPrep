using WebClient.Services.Home;
using WebClient.Services.Shared;

var builder = WebApplication.CreateBuilder(args);

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
