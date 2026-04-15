using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace gcmloader.Services;

/// <summary>
/// Optional fire-and-forget GitHub latest-release check (same API pattern as the companion hub).
/// Respects <c>github_update_check</c> in settings; logs only — no UI banner in gcmloader yet.
/// </summary>
public static class GitHubUpdateCheck
{
    private const string Owner = "toonymak1993";
    private const string Repo = "GameConsoleMode";

    public static void RunOptionalFireAndForget()
    {
        try
        {
            if (!AppSettings.Load<bool>("github_update_check"))
                return;
        }
        catch
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string? current = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                string? latest = await GetLatestReleaseTagAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(latest))
                {
                    Debug.WriteLine("[UpdateCheck] Could not read latest release.");
                    return;
                }

                if (IsNewer(current ?? "0.0.0.0", latest))
                    Debug.WriteLine($"[UpdateCheck] Newer release available: {latest} (running {current})");
                else
                    Debug.WriteLine($"[UpdateCheck] Up to date (latest tag {latest}).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateCheck] {ex.Message}");
            }
        });
    }

    private static async Task<string?> GetLatestReleaseTagAsync()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DeckTop-gcmloader");
        string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
        string json = await client.GetStringAsync(url).ConfigureAwait(false);
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("tag_name").GetString();
    }

    private static bool IsNewer(string current, string latest)
    {
        Version.TryParse(current, out Version? c);
        Version.TryParse(latest.TrimStart('v'), out Version? l);
        if (c == null || l == null) return false;
        return l > c;
    }
}
