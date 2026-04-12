using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using ProjectCallisto.API.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>                                                                                                                                                                                           
{                                                                                                                                                                                                                                     
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024; // 64KB                                                                                                                                                                    
});            
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.Configure<MicrosoftGraphOptions>(
    builder.Configuration.GetSection("AzureAd"));

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
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();