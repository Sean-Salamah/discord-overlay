using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordOverlay;

public class DiscordAuth
{
    private const string TokenUrl = "https://discord.com/api/oauth2/token";

    private static readonly HttpClient http = CreateHttpClient();

    private readonly Config config;
    private readonly string tokenPath;

    public DiscordAuth(Config config)
    {
        this.config = config;
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-overlay");
        tokenPath = Path.Combine(folder, "token.json");
    }

    public async Task<string?> GetSavedAccessTokenAsync(CancellationToken cancellationToken)
    {
        SavedToken? saved = LoadSavedToken();
        if (saved == null)
        {
            return null;
        }

        if (saved.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return saved.AccessToken;
        }

        try
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = saved.RefreshToken
            };
            return await RequestTokenAsync(form, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"Could not refresh the saved token: {ex.Message}");
            ClearSavedToken();
            return null;
        }
    }

    public Task<string> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code
        };

        if (!string.IsNullOrWhiteSpace(config.RedirectUri))
        {
            form["redirect_uri"] = config.RedirectUri;
        }

        return RequestTokenAsync(form, cancellationToken);
    }

    public void ClearSavedToken()
    {
        if (File.Exists(tokenPath))
        {
            File.Delete(tokenPath);
        }
    }

    private async Task<string> RequestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        form["client_id"] = config.ClientId;
        form["client_secret"] = config.ClientSecret;

        using var response = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new DiscordException($"Discord rejected the token request: {json}");
        }

        TokenResponse token = JsonSerializer.Deserialize<TokenResponse>(json)
            ?? throw new DiscordException("Discord sent back an empty token response.");

        SaveToken(new SavedToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)
        });

        return token.AccessToken;
    }

    private SavedToken? LoadSavedToken()
    {
        if (!File.Exists(tokenPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SavedToken>(File.ReadAllText(tokenPath));
        }
        catch (JsonException)
        {
            ClearSavedToken();
            return null;
        }
    }

    private void SaveToken(SavedToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(tokenPath)!);
        File.WriteAllText(tokenPath, JsonSerializer.Serialize(token));

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tokenPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordOverlay (https://github.com/Sean-Salamah/discord-overlay, 1.0)");
        return client;
    }
}

internal class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

internal class SavedToken
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
