using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.API.Services;

public interface IMicrosoftGraphService
{
    Task<Dictionary<string, PresenceResult>> GetPresenceAsync(MicrosoftConnection connection, List<string> userIds);
}

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly HttpClient _httpClient;
    private readonly IMicrosoftTokenService _tokenService;

    public MicrosoftGraphService(IHttpClientFactory httpClientFactory, IMicrosoftTokenService tokenService)
    {
        _httpClient = httpClientFactory.CreateClient();
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
