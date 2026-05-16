using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models; // <-- your DbContext + Models namespace
using Microsoft.AspNetCore.Authentication.Cookies;
using ProjectTallify.Services;
using ProjectTallify.Hubs;


var builder = WebApplication.CreateBuilder(args);

// ======================================================
// DATABASE (MySQL via Pomelo)
// ======================================================
var connectionString = builder.Configuration.GetConnectionString("TallifyDb");

builder.Services.AddDbContext<TallifyDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));


// ======================================================
// MVC + Controllers + Views
// ======================================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR(); // Add SignalR


// ======================================================
// (OPTIONAL) SIMPLE COOKIE AUTH
// If you want login sessions; you can remove if not needed
// ======================================================
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";       // where to redirect if not logged in
        options.AccessDeniedPath = "/Auth/Denied";
    });


// ======================================================
// SESSION (for temporary storage)
// IMPORTANT: Session must be enabled BEFORE UseRouting()
// ======================================================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

// Scoring Service
builder.Services.AddTransient<IScoringService, ScoringService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddTransient<IReportService, ReportService>();


// ======================================================
// BUILD APP
// ======================================================
var app = builder.Build();


// ======================================================
// ERROR HANDLING + SECURITY
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();


// ======================================================
// ROUTING + AUTH + SESSION
// ======================================================
app.UseRouting();

app.UseSession();            // MUST BE BEFORE Authorization
app.UseAuthentication();      // if using cookie login
app.UseAuthorization();


// ======================================================
// DEFAULT ROUTE
// Your app starts at the Login screen
// ======================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.MapHub<NotificationHub>("/notificationHub"); // Map SignalR Hub

app.Run();
