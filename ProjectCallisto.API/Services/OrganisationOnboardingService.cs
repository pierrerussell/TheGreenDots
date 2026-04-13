using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ProjectCallisto.API.Configuration;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Services;

public interface IOrganisationOnboardingService
{
    Task<Organisation> ConnectOrganisationAsync(User user, string authCode);
}

public class OrganisationOnboardingService : IOrganisationOnboardingService
{
    private readonly AppDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphOptions _options;

    public OrganisationOnboardingService(
        AppDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        IOptions<MicrosoftGraphOptions> options)
    {
        _dbContext = dbContext;
        _httpClient = httpClientFactory.CreateClient();
        _options = options.Value;
    }

    public async Task<Organisation> ConnectOrganisationAsync(User user, string authCode)
    {
        var token = await ExchangeCodeForTokenAsync(authCode);
        var tenantId = ExtractTenantIdFromToken(token.AccessToken);
        var orgName = await FetchOrganisationNameAsync(token.AccessToken);

        var connection = await CreateMicrosoftConnectionAsync(user.Id, tenantId, token);
        var organisation = await CreateOrganisationAsync(orgName, tenantId, connection.Id);
        await CreateOrganisationUserAsync(organisation.Id, user.Id);

        await _dbContext.SaveChangesAsync();

        return organisation;
    }

    private async Task<MicrosoftTokenResponse> ExchangeCodeForTokenAsync(string authCode)
    {
        var formBody = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = authCode,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync(
            "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
            formBody);

        var tokenString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token exchange failed: {tokenString}");
        }

        return JsonSerializer.Deserialize<MicrosoftTokenResponse>(tokenString)
               ?? throw new InvalidOperationException("Failed to deserialize token response");
    }

    private static string ExtractTenantIdFromToken(string accessToken)
    {
        var tokenParts = accessToken.Split('.');
        var payload = tokenParts[1];
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var jsonBytes = Convert.FromBase64String(payload);
        var claims = JsonSerializer.Deserialize<JsonElement>(jsonBytes);
        return claims.GetProperty("tid").GetString()
               ?? throw new InvalidOperationException("Tenant ID not found in token");
    }

    private async Task<string> FetchOrganisationNameAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/organization");
        var orgJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to fetch organisation: {orgJson}");
        }

        var orgData = JsonSerializer.Deserialize<JsonElement>(orgJson);
        return orgData.GetProperty("value")[0].GetProperty("displayName").GetString()
               ?? "Unknown Organisation";
    }

    private async Task<MicrosoftConnection> CreateMicrosoftConnectionAsync(
        Guid userId,
        string tenantId,
        MicrosoftTokenResponse token)
    {
        var connection = new MicrosoftConnection
        {
            UserId = userId,
            TenantId = tenantId,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.MicrosoftConnections.AddAsync(connection);
        return connection;
    }

    private async Task<Organisation> CreateOrganisationAsync(
        string name,
        string tenantId,
        Guid activeConnectionId)
    {
        var organisation = new Organisation
        {
            Name = name,
            TenantId = tenantId,
            ActiveConnectionId = activeConnectionId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _dbContext.Organisations.AddAsync(organisation);
        return organisation;
    }

    private async Task CreateOrganisationUserAsync(Guid organisationId, Guid userId)
    {
        var organisationUser = new OrganisationUser
        {
            OrganisationId = organisationId,
            UserId = userId
        };

        await _dbContext.OrganisationUsers.AddAsync(organisationUser);
    }
}

internal class MicrosoftTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
