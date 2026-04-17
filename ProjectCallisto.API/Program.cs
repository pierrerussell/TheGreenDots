using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ProjectCallisto.API.BackgroundServices;
using ProjectCallisto.API.Configuration;
using ProjectCallisto.API.Services;
using ProjectCallisto.EfCore;

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
builder.Services.AddHostedService<PresencePollingService>();

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