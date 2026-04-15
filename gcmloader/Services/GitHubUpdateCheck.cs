using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
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
    private static readonly HttpClient Client = CreateClient();

    public static void RunOptionalFireAndForget()
    {
        try
        {
            if (!AppSettings.Load<bool>("github_update_check"))
                return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateCheck] Disabled due to settings read error: {ex.Message}");
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
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[UpdateCheck] Request timed out.");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[UpdateCheck] Invalid JSON response: {ex.Message}");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[UpdateCheck] HTTP error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateCheck] {ex.Message}");
            }
        });
    }

    private static async Task<string?> GetLatestReleaseTagAsync()
    {
        string url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var response = await Client.GetAsync(url, timeoutCts.Token).ConfigureAwait(false);

        if ((int)response.StatusCode == 403)
        {
            string? remaining = TryGetHeaderValue(response, "X-RateLimit-Remaining");
            string? resetEpoch = TryGetHeaderValue(response, "X-RateLimit-Reset");
            if (remaining == "0" && long.TryParse(resetEpoch, NumberStyles.Integer, CultureInfo.InvariantCulture, out long epoch))
            {
                DateTimeOffset resetTime = DateTimeOffset.FromUnixTimeSeconds(epoch).ToLocalTime();
                Debug.WriteLine($"[UpdateCheck] GitHub API rate-limited. Reset at {resetTime:yyyy-MM-dd HH:mm:ss zzz}.");
            }
            else
            {
                Debug.WriteLine("[UpdateCheck] GitHub API request forbidden (403).");
            }
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            Debug.WriteLine($"[UpdateCheck] HTTP {(int)response.StatusCode} {response.ReasonPhrase} while checking releases.");
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
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

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DeckTop-gcmloader");
        return client;
    }

    private static string? TryGetHeaderValue(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            foreach (string value in values)
            {
                return value;
            }
        }

        return null;
    }
}
