using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SteamLoader.App.Models;

namespace SteamLoader.App.Infrastructure.Hltb;

public sealed class HltbService
{
    private const string SearchUrl = "https://howlongtobeat.com/api/bleed";
    private const string SearchInitUrl = "https://howlongtobeat.com/api/bleed/init";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HltbSettingsStore _settingsStore;
    private readonly object _gate = new();

    public HltbService(HltbSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public HltbSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return BuildSnapshot(_settingsStore.Load());
        }
    }

    public HltbSnapshot ToggleSetting(string key)
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            var settings = configuration.Settings;

            switch (key.Trim().ToLowerInvariant())
            {
                case "enabled":
                    settings.Enabled = !settings.Enabled;
                    break;
                case "show-main-story":
                    settings.ShowMainStory = !settings.ShowMainStory;
                    break;
                case "show-main-plus":
                    settings.ShowMainPlus = !settings.ShowMainPlus;
                    break;
                case "show-completionist":
                    settings.ShowCompletionist = !settings.ShowCompletionist;
                    break;
                case "show-all-styles":
                    settings.ShowAllStyles = !settings.ShowAllStyles;
                    break;
                case "show-view-details":
                    settings.ShowViewDetails = !settings.ShowViewDetails;
                    break;
                default:
                    throw new InvalidOperationException("The requested HLTB setting could not be found.");
            }

            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public HltbSnapshot ClearCache()
    {
        lock (_gate)
        {
            var configuration = _settingsStore.Load();
            configuration.Cache.Clear();
            _settingsStore.Save(configuration);
            return BuildSnapshot(configuration);
        }
    }

    public async Task<HltbGameSnapshot> GetGameAsync(string title, int? appId, CancellationToken cancellationToken)
    {
        var requestedTitle = title?.Trim() ?? string.Empty;
        var normalizedTitle = NormalizeTitle(requestedTitle);
        HltbConfiguration configuration;

        lock (_gate)
        {
            configuration = _settingsStore.Load();
        }

        var settingsState = BuildSettingsState(configuration);
        if (!configuration.Settings.Enabled || (string.IsNullOrWhiteSpace(normalizedTitle) && !appId.HasValue))
        {
            return new HltbGameSnapshot(
                RequestedTitle: requestedTitle,
                MatchedTitle: string.Empty,
                AppId: appId,
                GameId: null,
                MainStory: "--",
                MainPlus: "--",
                Completionist: "--",
                AllStyles: "--",
                DetailUrl: string.Empty,
                Found: false,
                Cached: false,
                Settings: settingsState,
                ErrorMessage: null);
        }

        var cacheKey = BuildCacheKey(requestedTitle, appId);
        if (configuration.Cache.TryGetValue(cacheKey, out var cachedEntry) &&
            cachedEntry is not null &&
            !NeedsRefresh(cachedEntry.LastUpdatedAtUtc))
        {
            return BuildGameSnapshot(cachedEntry, settingsState, cached: true);
        }

        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return new HltbGameSnapshot(
                RequestedTitle: requestedTitle,
                MatchedTitle: string.Empty,
                AppId: appId,
                GameId: null,
                MainStory: "--",
                MainPlus: "--",
                Completionist: "--",
                AllStyles: "--",
                DetailUrl: string.Empty,
                Found: false,
                Cached: false,
                Settings: settingsState,
                ErrorMessage: null);
        }

        try
        {
            var fetchedEntry = await FetchFromHltbAsync(requestedTitle, appId, cancellationToken);

            lock (_gate)
            {
                var latestConfiguration = _settingsStore.Load();
                latestConfiguration.Cache[cacheKey] = fetchedEntry;
                _settingsStore.Save(latestConfiguration);
                settingsState = BuildSettingsState(latestConfiguration);
            }

            return BuildGameSnapshot(fetchedEntry, settingsState, cached: false);
        }
        catch (Exception exception)
        {
            if (cachedEntry is not null)
            {
                return BuildGameSnapshot(cachedEntry, settingsState, cached: true) with
                {
                    ErrorMessage = exception.Message
                };
            }

            return new HltbGameSnapshot(
                RequestedTitle: requestedTitle,
                MatchedTitle: string.Empty,
                AppId: appId,
                GameId: null,
                MainStory: "--",
                MainPlus: "--",
                Completionist: "--",
                AllStyles: "--",
                DetailUrl: string.Empty,
                Found: false,
                Cached: false,
                Settings: settingsState,
                ErrorMessage: exception.Message);
        }
    }

    private static bool NeedsRefresh(DateTimeOffset lastUpdatedAtUtc)
    {
        return DateTimeOffset.UtcNow - lastUpdatedAtUtc > CacheLifetime;
    }

    private static HltbSnapshot BuildSnapshot(HltbConfiguration configuration)
    {
        var settingsState = BuildSettingsState(configuration);
        var enabledStatCount = 0;
        enabledStatCount += configuration.Settings.ShowMainStory ? 1 : 0;
        enabledStatCount += configuration.Settings.ShowMainPlus ? 1 : 0;
        enabledStatCount += configuration.Settings.ShowCompletionist ? 1 : 0;
        enabledStatCount += configuration.Settings.ShowAllStyles ? 1 : 0;

        var statusText = configuration.Settings.Enabled
            ? $"{enabledStatCount} stat categories enabled - {configuration.Cache.Count} cached game{(configuration.Cache.Count == 1 ? string.Empty : "s")}."
            : "Game page stats are currently disabled.";

        return new HltbSnapshot(settingsState, statusText);
    }

    private static HltbSettingsState BuildSettingsState(HltbConfiguration configuration)
    {
        return new HltbSettingsState(
            configuration.Settings.Enabled,
            configuration.Settings.ShowMainStory,
            configuration.Settings.ShowMainPlus,
            configuration.Settings.ShowCompletionist,
            configuration.Settings.ShowAllStyles,
            configuration.Settings.ShowViewDetails,
            configuration.Cache.Count);
    }

    private static HltbGameSnapshot BuildGameSnapshot(
        HltbCacheEntry entry,
        HltbSettingsState settings,
        bool cached)
    {
        return new HltbGameSnapshot(
            RequestedTitle: entry.RequestedTitle,
            MatchedTitle: string.IsNullOrWhiteSpace(entry.MatchedTitle) ? entry.RequestedTitle : entry.MatchedTitle,
            AppId: entry.AppId,
            GameId: entry.GameId,
            MainStory: FormatHours(entry.MainStoryHours),
            MainPlus: FormatHours(entry.MainPlusHours),
            Completionist: FormatHours(entry.CompletionistHours),
            AllStyles: FormatHours(entry.AllStylesHours),
            DetailUrl: entry.DetailUrl,
            Found: entry.Found,
            Cached: cached,
            Settings: settings,
            ErrorMessage: null);
    }

    private static async Task<HltbCacheEntry> FetchFromHltbAsync(
        string requestedTitle,
        int? appId,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(18)
        };

        var authState = await FetchAuthTokenAsync(httpClient, cancellationToken)
            ?? throw new InvalidOperationException("HowLongToBeat did not return an auth token.");

        using var request = new HttpRequestMessage(HttpMethod.Post, SearchUrl);
        request.Headers.TryAddWithoutValidation("Origin", "https://howlongtobeat.com");
        request.Headers.Referrer = new Uri("https://howlongtobeat.com/");
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        request.Headers.TryAddWithoutValidation("x-auth-token", authState.Token);
        request.Headers.TryAddWithoutValidation("x-hp-key", authState.HpKey);
        request.Headers.TryAddWithoutValidation("x-hp-val", authState.HpVal);
        request.Content = new StringContent(
            JsonSerializer.Serialize(BuildSearchPayload(requestedTitle, authState), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<HltbSearchResponse>(stream, JsonOptions, cancellationToken)
            ?? new HltbSearchResponse();

        var bestMatch = SelectBestMatch(requestedTitle, appId, payload.Data);
        if (bestMatch is null)
        {
            return new HltbCacheEntry
            {
                RequestedTitle = requestedTitle,
                MatchedTitle = requestedTitle,
                AppId = appId,
                Found = false,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            };
        }

        return new HltbCacheEntry
        {
            RequestedTitle = requestedTitle,
            MatchedTitle = bestMatch.GameName ?? requestedTitle,
            AppId = appId,
            GameId = bestMatch.GameId,
            MainStoryHours = ToHours(bestMatch.CompMain),
            MainPlusHours = ToHours(bestMatch.CompPlus),
            CompletionistHours = ToHours(bestMatch.Comp100),
            AllStylesHours = ToHours(bestMatch.CompAll),
            DetailUrl = $"https://howlongtobeat.com/game/{bestMatch.GameId}",
            Found = true,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static async Task<HltbSearchInitResponse?> FetchAuthTokenAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{SearchInitUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        request.Headers.TryAddWithoutValidation("Origin", "https://howlongtobeat.com");
        request.Headers.Referrer = new Uri("https://howlongtobeat.com/");
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<HltbSearchInitResponse>(stream, JsonOptions, cancellationToken);
    }

    private static Dictionary<string, object?> BuildSearchPayload(string title, HltbSearchInitResponse authState)
    {
        var payload = new Dictionary<string, object?>
        {
            ["searchType"] = "games",
            ["searchTerms"] = title
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ["searchPage"] = 1,
            ["size"] = 20,
            ["searchOptions"] = new Dictionary<string, object?>
            {
                ["games"] = new Dictionary<string, object?>
                {
                    ["userId"] = 0,
                    ["platform"] = string.Empty,
                    ["sortCategory"] = "popular",
                    ["rangeCategory"] = "main",
                    ["rangeTime"] = new Dictionary<string, object?>
                    {
                        ["min"] = null,
                        ["max"] = null,
                    },
                    ["gameplay"] = new Dictionary<string, object?>
                    {
                        ["perspective"] = string.Empty,
                        ["flow"] = string.Empty,
                        ["genre"] = string.Empty,
                        ["difficulty"] = string.Empty,
                    },
                    ["rangeYear"] = new Dictionary<string, object?>
                    {
                        ["min"] = string.Empty,
                        ["max"] = string.Empty,
                    },
                    ["modifier"] = string.Empty,
                },
                ["users"] = new Dictionary<string, object?>
                {
                    ["sortCategory"] = "postcount",
                },
                ["lists"] = new Dictionary<string, object?>
                {
                    ["sortCategory"] = "follows",
                },
                ["filter"] = string.Empty,
                ["sort"] = 0,
                ["randomizer"] = 0,
            },
            ["useCache"] = true,
        };

        payload[authState.HpKey] = authState.HpVal;
        return payload;
    }

    private static HltbSearchResult? SelectBestMatch(
        string requestedTitle,
        int? appId,
        IReadOnlyList<HltbSearchResult> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        if (appId.HasValue)
        {
            var bySteamId = results.FirstOrDefault(result => result.ProfileSteam == appId.Value);
            if (bySteamId is not null)
            {
                return bySteamId;
            }
        }

        var normalizedRequestedTitle = NormalizeTitle(requestedTitle);
        var exactNameMatch = results.FirstOrDefault(result =>
            string.Equals(NormalizeTitle(result.GameName), normalizedRequestedTitle, StringComparison.OrdinalIgnoreCase));
        if (exactNameMatch is not null)
        {
            return exactNameMatch;
        }

        var aliasMatch = results.FirstOrDefault(result =>
            EnumerateAliases(result.GameAlias).Any(alias =>
                string.Equals(NormalizeTitle(alias), normalizedRequestedTitle, StringComparison.OrdinalIgnoreCase)));
        if (aliasMatch is not null)
        {
            return aliasMatch;
        }

        return results
            .OrderBy(result => ScoreResult(normalizedRequestedTitle, result))
            .ThenByDescending(result => result.CompAllCount)
            .FirstOrDefault();
    }

    private static int ScoreResult(string normalizedRequestedTitle, HltbSearchResult result)
    {
        var candidateNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.GameName))
        {
            candidateNames.Add(NormalizeTitle(result.GameName));
        }

        candidateNames.AddRange(EnumerateAliases(result.GameAlias).Select(NormalizeTitle));
        candidateNames = candidateNames
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateNames.Count == 0)
        {
            return int.MaxValue;
        }

        var bestScore = int.MaxValue;
        foreach (var candidateName in candidateNames)
        {
            var score = LevenshteinDistance(normalizedRequestedTitle, candidateName);
            if (candidateName.Contains(normalizedRequestedTitle, StringComparison.OrdinalIgnoreCase) ||
                normalizedRequestedTitle.Contains(candidateName, StringComparison.OrdinalIgnoreCase))
            {
                score = Math.Max(0, score - 2);
            }

            bestScore = Math.Min(bestScore, score);
        }

        return bestScore;
    }

    private static IEnumerable<string> EnumerateAliases(string? aliases)
    {
        if (string.IsNullOrWhiteSpace(aliases))
        {
            yield break;
        }

        foreach (var alias in aliases.Split([',', ';', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return alias;
        }
    }

    private static string BuildCacheKey(string title, int? appId)
    {
        return appId.HasValue && appId.Value > 0
            ? $"app:{appId.Value}"
            : $"title:{NormalizeTitle(title)}";
    }

    private static string NormalizeTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) || character == ' ' || character == '-' || character == '/')
            {
                builder.Append(character);
            }
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return 0;
        }

        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previousRow = new int[right.Length + 1];
        var currentRow = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previousRow[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            currentRow[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                currentRow[column] = Math.Min(
                    Math.Min(currentRow[column - 1] + 1, previousRow[column] + 1),
                    previousRow[column - 1] + substitutionCost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[right.Length];
    }

    private static double? ToHours(int seconds)
    {
        if (seconds <= 0)
        {
            return null;
        }

        return Math.Round(seconds / 3600d, 1, MidpointRounding.AwayFromZero);
    }

    private static string FormatHours(double? value)
    {
        return value.HasValue && value.Value > 0
            ? value.Value.ToString("0.0", CultureInfo.InvariantCulture)
            : "--";
    }

    private sealed class HltbSearchResponse
    {
        [JsonPropertyName("data")]
        public List<HltbSearchResult> Data { get; set; } = [];
    }

    private sealed class HltbSearchInitResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("hpKey")]
        public string HpKey { get; set; } = string.Empty;

        [JsonPropertyName("hpVal")]
        public string HpVal { get; set; } = string.Empty;
    }

    private sealed class HltbSearchResult
    {
        [JsonPropertyName("game_id")]
        public int GameId { get; set; }

        [JsonPropertyName("game_name")]
        public string GameName { get; set; } = string.Empty;

        [JsonPropertyName("game_alias")]
        public string GameAlias { get; set; } = string.Empty;

        [JsonPropertyName("comp_main")]
        public int CompMain { get; set; }

        [JsonPropertyName("comp_plus")]
        public int CompPlus { get; set; }

        [JsonPropertyName("comp_100")]
        public int Comp100 { get; set; }

        [JsonPropertyName("comp_all")]
        public int CompAll { get; set; }

        [JsonPropertyName("comp_all_count")]
        public int CompAllCount { get; set; }

        [JsonPropertyName("profile_steam")]
        public int ProfileSteam { get; set; }
    }
}
