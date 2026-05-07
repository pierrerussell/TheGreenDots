using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectCallisto.Domain.Organisations;

namespace ProjectCallisto.Application.Microsoft;

public interface IMicrosoftTokenService
{
    Task<MicrosoftConnection> RefreshTokenAsync(MicrosoftConnection connection, CancellationToken ct = default);
    Task<MicrosoftConnection?> GetValidConnectionAsync(Guid connectionId, CancellationToken ct = default);
}

public class MicrosoftTokenService : IMicrosoftTokenService
{
    private readonly HttpClient _httpClient;
    private readonly MicrosoftGraphOptions _options;
    private readonly IMicrosoftConnectionRepository _microsoftConnectionRepository;
    private readonly ILogger<MicrosoftTokenService> _logger;

    public MicrosoftTokenService(
        HttpClient httpClient,
        IOptions<MicrosoftGraphOptions> options,
        IMicrosoftConnectionRepository microsoftConnectionRepository,
        ILogger<MicrosoftTokenService> logger
        )
    {
        _httpClient = httpClient;
        _options = options.Value;
        _microsoftConnectionRepository = microsoftConnectionRepository;
        _logger = logger;
    }

    public async Task<MicrosoftConnection?> GetValidConnectionAsync(Guid connectionId, CancellationToken ct = default)
    {

        var connection = await _microsoftConnectionRepository.FindAsync(connectionId, ct); // await _dbContext.MicrosoftConnections.FindAsync([connectionId], ct);
        if (connection == null) return null;

        // Refresh if expiring within 5 minutes
        if (connection.ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            connection = await RefreshTokenAsync(connection, ct);
        }

        return connection;
    }

    public async Task<MicrosoftConnection> RefreshTokenAsync(MicrosoftConnection connection, CancellationToken ct = default)
    {
        var formBody = new FormUrlEncodedContent(new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = connection.RefreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = string.Join(" ", _options.Scopes)
        });

        var response = await _httpClient.PostAsync(
            "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
            formBody,
            ct);

        var tokenString = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Log detailed error server-side for debugging
            _logger.LogError(
                "Token refresh failed for connection {ConnectionId}. Status: {StatusCode}, Response: {Response}",
                connection.Id,
                response.StatusCode,
                tokenString);

            // Throw generic message to client (don't expose Microsoft's error response)
            throw new InvalidOperationException("Failed to refresh Microsoft authentication. The connection may need to be re-authorized.");
        }

        TokenResponse? token;
        try
        {
            token = JsonSerializer.Deserialize<TokenResponse>(tokenString);
        }
        catch (JsonException ex)
        {
            // Log detailed error server-side
            _logger.LogError(ex,
                "Failed to deserialize token response for connection {ConnectionId}. Response: {Response}",
                connection.Id,
                tokenString);

            // Throw generic message to client
            throw new InvalidOperationException("Failed to process Microsoft authentication response. Please try again or re-authorize the connection.");
        }

        if (token == null)
        {
            _logger.LogError("Token deserialization returned null for connection {ConnectionId}", connection.Id);
            throw new InvalidOperationException("Failed to process Microsoft authentication response. Please try again or re-authorize the connection.");
        }

        // Update connection with new tokens
        connection.AccessToken = token.AccessToken;
        connection.RefreshToken = token.RefreshToken;
        connection.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);

        await _microsoftConnectionRepository.SaveChangesAsync(ct);

        return connection;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
