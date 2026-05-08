using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ProjectCallisto.API.Authorization;
using ProjectCallisto.API.Middleware;


using ProjectCallisto.API.Services;
using ProjectCallisto.Application.Billing;
using ProjectCallisto.Application.Emails;
using ProjectCallisto.Application.Microsoft;
using ProjectCallisto.Application.Queues;
using ProjectCallisto.Application.Reports;
using ProjectCallisto.AzureQueue;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.EfCore;
using ProjectCallisto.EfCore.Microsoft;
using ProjectCallisto.Stripe;
using Stripe;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>                                                                                                                                                                                           
{                                                                                                                                                                                                                                     
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024; // 64KB                                                                                                                                                                    
});            
// Add services to the container.
builder.Services.AddMemoryCache();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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

// For stripe
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
builder.Services.AddScoped<IBillingService, StripeBillingService>();
builder.Services.AddSingleton<IStripeClient>(x =>
{
    var secretKey = builder.Configuration["Stripe:SecretKey"];
    return new StripeClient(secretKey);
});

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

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    // Default policy for all API endpoints
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100; // 100 requests
        limiterOptions.Window = TimeSpan.FromMinutes(1); // per minute
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10; // Queue up to 10 requests when limit exceeded
    });

    // Stricter policy for expensive operations (sample emails, report generation)
    options.AddFixedWindowLimiter("expensive", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5; // Only 5 requests
        limiterOptions.Window = TimeSpan.FromMinutes(1); // per minute
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 2; // Shorter queue
    });

    // Global limiter - prevents a single user from overwhelming the entire app
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Use user's subject ID if authenticated, otherwise IP address
        var userId = context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200, // 200 requests per user
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0 // No queuing for global limiter
        });
    });

    // Rejection status code
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();
var browserPath = Path.Combine(app.Environment.WebRootPath, "browser");

// Global exception handler - sanitize error messages in production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            if (exception != null)
            {
                // Log detailed error server-side with structured logging
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                var userId = context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

                logger.LogError(exception,
                    "Unhandled exception occurred. " +
                    "CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}, " +
                    "UserId: {UserId}, StatusCode: {StatusCode}",
                    context.TraceIdentifier,
                    context.Request.Path,
                    context.Request.Method,
                    userId ?? "anonymous",
                    context.Response.StatusCode);
            }

            // Return generic error to client (no stack traces or internal details)
            var response = new
            {
                error = "An error occurred processing your request. Please try again or contact support if the problem persists.",
                correlationId = context.TraceIdentifier // For support troubleshooting
            };

            await context.Response.WriteAsJsonAsync(response);
        });
    });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // In development, show detailed errors for debugging
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// Correlation ID for request tracing
app.UseCorrelationId();

// Rate limiting
app.UseRateLimiter();

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
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " + // Angular needs unsafe-inline and unsafe-eval
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " + // Angular needs unsafe-inline, allow Google Fonts
        "img-src 'self' data:; " + // Allow data URIs for inline images
        "font-src 'self' data: https://fonts.gstatic.com; " + // Allow Google Fonts and data URIs
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