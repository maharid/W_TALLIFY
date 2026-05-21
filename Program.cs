using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models; // <-- your DbContext + Models namespace
using ProjectTallify.Hubs;   // <-- where your NotificationHub is
using ProjectTallify.Services;

var builder = WebApplication.CreateBuilder(args);

// ==================================================
// 1. SERVICES
// ==================================================
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// DB: MySQL (Pomelo)
var connectionString = builder.Configuration.GetConnectionString("TallifyDb");
builder.Services.AddDbContext<TallifyDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Authentication (Cookie-based for SignalR support)
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });

// Real-time: SignalR
builder.Services.AddSignalR();

// Business Services
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// ==================================================
// 2. MIDDLEWARE
// ==================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ==================================================
// 3. ROUTES
// ==================================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.MapHub<NotificationHub>("/notificationHub"); // Map SignalR Hub

app.Run();
