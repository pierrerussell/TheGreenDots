using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ProjectCallisto.API.Authorization;
using ProjectCallisto.API.BackgroundServices;

using ProjectCallisto.API.Services;
using ProjectCallisto.Application.Microsoft;
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

builder.Services.AddHostedService<PresencePollingService>();

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

var app = builder.Build();
var browserPath = Path.Combine(app.Environment.WebRootPath, "browser");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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