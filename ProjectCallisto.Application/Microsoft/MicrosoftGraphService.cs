using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Microsoft;

public interface IMicrosoftGraphService
{
    Task<Dictionary<string, PresenceResult>> GetPresenceAsync(MicrosoftConnection connection, List<string> userIds);
    Task<List<GraphUser>> GetUsersAsync(MicrosoftConnection connection);
}

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly HttpClient _httpClient;
    private readonly IMicrosoftTokenService _tokenService;

    public MicrosoftGraphService(HttpClient httpClient, IMicrosoftTokenService tokenService)
    {
        _httpClient = httpClient;
        _tokenService = tokenService;
    }

    public async Task<Dictionary<string, PresenceResult>> GetPresenceAsync(MicrosoftConnection connection, List<string> userIds)
    {
        if (userIds.Count == 0)
            return new Dictionary<string, PresenceResult>();

        // Ensure token is valid
        var validConnection = await _tokenService.GetValidConnectionAsync(connection.Id);
        if (validConnection == null)
            return new Dictionary<string, PresenceResult>();

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", validConnection.AccessToken);

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
            return new Dictionary<string, PresenceResult>();
        }

        var data = JsonSerializer.Deserialize<GraphPresenceListResponse>(json);
        return data?.Value?.ToDictionary(
            p => p.Id,
            p => new PresenceResult
            {
                Availability = p.Availability ?? "Offline",
                Activity = p.Activity
            }
        ) ?? new Dictionary<string, PresenceResult>();
    }

    public async Task<List<GraphUser>> GetUsersAsync(MicrosoftConnection connection)
    {
        var users = new List<GraphUser>();

        // Ensure token is valid
        var validConnection = await _tokenService.GetValidConnectionAsync(connection.Id);
        if (validConnection == null)
            return users;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", validConnection.AccessToken);

        var url = "https://graph.microsoft.com/v1.0/users?$select=id,displayName,mail,jobTitle&$top=999";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                break;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<GraphUsersResponse>(json);

            if (data?.Value != null)
            {
                users.AddRange(data.Value);
            }

            url = data?.NextLink ?? string.Empty;
        }

        return users;
    }
}

public class PresenceResult
{
    public string Availability { get; set; } = "Offline";
    public string? Activity { get; set; }
}

internal class GraphPresenceListResponse
{
    [JsonPropertyName("value")]
    public List<GraphPresenceItem>? Value { get; set; }
}

internal class GraphPresenceItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }

    [JsonPropertyName("activity")]
    public string? Activity { get; set; }
}

public class GraphUser
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
