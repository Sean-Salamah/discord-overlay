using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordOverlay;

public class Config
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; } = "";

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    public static Config Load()
    {
        string path = FindConfigFile()
            ?? throw new FileNotFoundException("config.json not found. Copy config.example.json to config.json and add your Discord app details.");

        Config? config = JsonSerializer.Deserialize<Config>(File.ReadAllText(path));

        if (config == null || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.ClientSecret))
        {
            throw new InvalidDataException("config.json needs both client_id and client_secret.");
        }

        return config;
    }

    private static string? FindConfigFile()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "config.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "config.json")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
