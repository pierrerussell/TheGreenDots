using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ProjectCallisto.API.Authorization;


using ProjectCallisto.API.Services;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.AzureQueue;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using ProjectCallisto.EfCore.Microsoft;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>                                                                                                                                                                                           
{                                                                                                                                                                                                                                     
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024; // 64KB                                                                                                                                                                    
});            
// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.Configure<MicrosoftGraphOptions>(
    builder.Configuration.GetSection("AzureAd"));
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddScoped<IOrganisationOnboardingService, OrganisationOnboardingService>();
builder.Services.AddScoped<IMicrosoftTokenService, MicrosoftTokenService>();
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
builder.Services.AddScoped<IMicrosoftConnectionRepository, MicrosoftConnectionRepository>();

// Required for authorization handler to access route parameters
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["Auth0:Authority"];
        options.ClientId = builder.Configuration["Auth0:ClientId"];
        options.ClientSecret = builder.Configuration["Auth0:ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.CallbackPath = "/signin-oidc";
        options.MapInboundClaims = false;
    });

// Add permission-based authorization
builder.Services.AddAuthorization(options =>
{
    // Create a policy for each permission
    options.AddPolicy(nameof(Permission.ViewDashboard), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.ViewDashboard)));

    options.AddPolicy(nameof(Permission.ManageSeats), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.ManageSeats)));

    options.AddPolicy(nameof(Permission.ManageBilling), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.ManageBilling)));

    options.AddPolicy(nameof(Permission.ExportData), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.ExportData)));

    options.AddPolicy(nameof(Permission.InviteUsers), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.InviteUsers)));

    options.AddPolicy(nameof(Permission.ManageSettings), policy =>
        policy.Requirements.Add(new PermissionRequirement(Permission.ManageSettings)));
});

// Register authorization handler
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Azure Queue
builder.Services.Configure<AzureQueueOptions>(builder.Configuration.GetSection("AzureQueue"));
builder.Services.AddScoped<IQueueService<EmailMessage>, AzureQueueService<EmailMessage>>();

// Report Services
builder.Services.AddScoped<IReportCalculationService, ReportCalculationService>();
builder.Services.AddScoped<IPresenceBreakdownCalculator, PresenceBreakdownCalculator>();
builder.Services.AddScoped<IInsightDetectionService, InsightDetectionService>();
builder.Services.AddScoped<ReportEmailHtmlGenerator>();

var app = builder.Build();
var browserPath = Path.Combine(app.Environment.WebRootPath, "browser");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Security headers
app.Use(async (context, next) =>
{
    // Prevent MIME type sniffing
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Prevent clickjacking attacks
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // Enable XSS protection in legacy browsers
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    // Enforce HTTPS (max-age=31536000 = 1 year)
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

    // Content Security Policy - allow scripts/styles from same origin only
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " + // Angular needs unsafe-inline for component styles
        "img-src 'self' data:; " + // Allow data URIs for inline images
        "font-src 'self'; " +
        "connect-src 'self'; " + // Allow API calls to same origin
        "frame-ancestors 'none'; " + // Same as X-Frame-Options: DENY
        "base-uri 'self'; " +
        "form-action 'self'");

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles(new DefaultFilesOptions()
{
    FileProvider = new PhysicalFileProvider(browserPath)
}); 

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(browserPath)
});

app.MapControllers();

app.MapFallbackToFile("/browser/index.html");

app.Run();