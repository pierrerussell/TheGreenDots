using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ProjectCallisto.API.Configuration;
using ProjectCallisto.Domain.Organisations;
using ProjectCallisto.Domain.Users;
using ProjectCallisto.EfCore;

namespace ProjectCallisto.API.Services;

public interface IOrganisationOnboardingService
{
    Task<OnboardingResult> ConnectOrganisationAsync(User user, string authCode);
}

public class OnboardingResult
{
    public Guid OrganisationId { get; init; }
    public string OrganisationName { get; init; } = string.Empty;
    public List<MemberWithPresence> Members { get; init; } = [];
}

public class MemberWithPresence
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? JobTitle { get; init; }
    public string Availability { get; init; } = "Offline";
    public string? Activity { get; init; }
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

    public async Task<OnboardingResult> ConnectOrganisationAsync(User user, string authCode)
    {
        var token = await ExchangeCodeForTokenAsync(authCode);
        var tenantId = ExtractTenantIdFromToken(token.AccessToken);
        var orgName = await FetchOrganisationNameAsync(token.AccessToken);

        var connection = await CreateMicrosoftConnectionAsync(user.Id, tenantId, token);
        var organisation = await CreateOrganisationAsync(orgName, tenantId, connection.Id);
        await CreateOrganisationUserAsync(organisation.Id, user.Id);

        // Fetch and store tenant members
        var graphUsers = await FetchTenantUsersAsync(token.AccessToken);
        var tenantMembers = await CreateTenantMembersAsync(organisation.Id, graphUsers);

        await _dbContext.SaveChangesAsync();

        // Fetch presence for all members (after save so we have IDs)
        var memberIds = graphUsers.Select(u => u.Id).ToList();
        var presenceMap = await FetchPresenceAsync(token.AccessToken, memberIds);

        // Build result with presence
        var membersWithPresence = tenantMembers.Select(m => new MemberWithPresence
        {
            Id = m.Id,
            DisplayName = m.DisplayName,
            Email = m.Email,
            JobTitle = m.JobTitle,
            Availability = presenceMap.GetValueOrDefault(m.MicrosoftUserId)?.Availability ?? "Offline",
            Activity = presenceMap.GetValueOrDefault(m.MicrosoftUserId)?.Activity
        }).ToList();

        return new OnboardingResult
        {
            OrganisationId = organisation.Id,
            OrganisationName = organisation.Name,
            Members = membersWithPresence
        };
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

    private async Task<List<GraphUser>> FetchTenantUsersAsync(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var users = new List<GraphUser>();
        var url = "https://graph.microsoft.com/v1.0/users?$select=id,displayName,mail,jobTitle&$top=999";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Failed to fetch users: {json}");
            }

            var data = JsonSerializer.Deserialize<GraphUsersResponse>(json);
            if (data?.Value != null)
            {
                users.AddRange(data.Value);
            }

            url = data?.NextLink;
        }

        return users;
    }

    private async Task<List<TenantMember>> CreateTenantMembersAsync(Guid organisationId, List<GraphUser> graphUsers)
    {
        var tenantMembers = graphUsers.Select(u => new TenantMember
        {
            OrganisationId = organisationId,
            MicrosoftUserId = u.Id,
            DisplayName = u.DisplayName ?? "Unknown",
            Email = u.Mail,
            JobTitle = u.JobTitle,
            CreatedAt = DateTimeOffset.UtcNow
        }).ToList();

        await _dbContext.TenantMembers.AddRangeAsync(tenantMembers);
        return tenantMembers;
    }

    private async Task<Dictionary<string, PresenceInfo>> FetchPresenceAsync(string accessToken, List<string> userIds)
    {
        if (userIds.Count == 0)
            return new Dictionary<string, PresenceInfo>();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var requestBody = new { ids = userIds };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://graph.microsoft.com/v1.0/communications/getPresencesByUserId",
            content);

        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Don't fail onboarding if presence fetch fails - just return empty
            return new Dictionary<string, PresenceInfo>();
        }

        var data = JsonSerializer.Deserialize<GraphPresenceResponse>(json);
        return data?.Value?.ToDictionary(p => p.Id, p => new PresenceInfo
        {
            Availability = p.Availability ?? "Offline",
            Activity = p.Activity
        }) ?? new Dictionary<string, PresenceInfo>();
    }
}

internal class GraphUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("mail")]
    public string? Mail { get; set; }

    [JsonPropertyName("jobTitle")]
    public string? JobTitle { get; set; }
}

internal class GraphUsersResponse
{
    [JsonPropertyName("value")]
    public List<GraphUser>? Value { get; set; }

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; set; }
}

internal class GraphPresence
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("activity")]
    public string? Activity { get; set; }
}

internal class GraphPresenceResponse
{
    [JsonPropertyName("value")]
    public List<GraphPresence>? Value { get; set; }
}

internal class PresenceInfo
{
    public string Availability { get; set; } = "Offline";
    public string? Activity { get; set; }
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
