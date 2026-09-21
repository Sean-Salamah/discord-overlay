using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace DiscordOverlay;

public static class AvatarLoader
{
    private static readonly HttpClient http = new();
    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> cache = new();

    public static Task<Bitmap?> LoadAsync(string userId, string? avatarHash)
    {
        string url = avatarHash != null
            ? $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=64"
            : $"https://cdn.discordapp.com/embed/avatars/{DefaultAvatarIndex(userId)}.png";

        return cache.GetOrAdd(url, DownloadAsync);
    }

    private static int DefaultAvatarIndex(string userId)
    {
        return ulong.TryParse(userId, out ulong id) ? (int)((id >> 22) % 6) : 0;
    }

    private static async Task<Bitmap?> DownloadAsync(string url)
    {
        try
        {
            byte[] bytes = await http.GetByteArrayAsync(url);
            using var imageStream = new MemoryStream(bytes);
            return new Bitmap(imageStream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not load avatar {url}: {ex.Message}");
            return null;
        }
    }
}
