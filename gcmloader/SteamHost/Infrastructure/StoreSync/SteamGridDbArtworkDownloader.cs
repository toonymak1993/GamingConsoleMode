using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SteamLoader.App.Infrastructure.StoreSync;

internal sealed class SteamGridDbArtworkDownloader
{
    public const string BuiltInApiKey = "96b06c7e805c21ee48af894587118c4c";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ArtworkSlot[] ArtworkSlots =
    [
        new(
            SlotName: "library capsule",
            RequestPath: "grids/game/{0}?types=static&dimensions=920x430&mimes=image/png,image/jpeg",
            FileStemBuilder: gridId => gridId),
        new(
            SlotName: "portrait",
            RequestPath: "grids/game/{0}?types=static&dimensions=600x900&mimes=image/png,image/jpeg",
            FileStemBuilder: gridId => $"{gridId}p"),
        new(
            SlotName: "hero",
            RequestPath: "heroes/game/{0}?types=static&dimensions=1920x620&mimes=image/png,image/jpeg",
            FileStemBuilder: gridId => $"{gridId}_hero"),
        new(
            SlotName: "logo",
            RequestPath: "logos/game/{0}?types=static&mimes=image/png",
            FileStemBuilder: gridId => $"{gridId}_logo"),
        new(
            SlotName: "icon",
            RequestPath: "icons/game/{0}?types=static&dimensions=256&mimes=image/png,image/vnd.microsoft.icon",
            FileStemBuilder: gridId => $"{gridId}-icon"),
    ];

    public async Task<StoreSyncArtworkSummary> DownloadAsync(
        string gridDirectory,
        IReadOnlyList<StoreSyncArtworkTarget> targets,
        string apiKey,
        bool preferAnimatedArtwork,
        CancellationToken cancellationToken)
    {
        _ = preferAnimatedArtwork;

        if (targets.Count == 0 || string.IsNullOrWhiteSpace(apiKey))
        {
            return new StoreSyncArtworkSummary(0, 0);
        }

        Directory.CreateDirectory(gridDirectory);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://www.steamgriddb.com/api/v2/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var searchCache = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        var updatedTitleCount = 0;
        var updatedFileCount = 0;

        foreach (var target in targets
                     .GroupBy(target => target.AppId)
                     .Select(group => group.First()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var gameId = await FindGameIdAsync(httpClient, target.Title, searchCache, cancellationToken);
                if (!gameId.HasValue)
                {
                    continue;
                }

                var updatedFilesForTitle = await DownloadArtworkSetAsync(
                    httpClient,
                    gridDirectory,
                    SteamShortcutIds.BuildGridId(target.AppId),
                    gameId.Value,
                    cancellationToken);

                if (updatedFilesForTitle > 0)
                {
                    updatedTitleCount++;
                    updatedFileCount += updatedFilesForTitle;
                }
            }
            catch
            {
            }
        }

        return new StoreSyncArtworkSummary(updatedTitleCount, updatedFileCount);
    }

    public string GetEffectiveApiKey(string? configuredApiKey)
    {
        return string.IsNullOrWhiteSpace(configuredApiKey)
            ? BuiltInApiKey
            : configuredApiKey.Trim();
    }

    public string GetPreview(string? configuredApiKey)
    {
        if (string.IsNullOrWhiteSpace(configuredApiKey))
        {
            return "Built in";
        }

        var trimmedApiKey = configuredApiKey.Trim();
        return trimmedApiKey.Length <= 6
            ? "Configured"
            : $"Configured ({trimmedApiKey[^4..]})";
    }

    private async Task<int?> FindGameIdAsync(
        HttpClient httpClient,
        string title,
        IDictionary<string, int?> searchCache,
        CancellationToken cancellationToken)
    {
        foreach (var term in BuildSearchTerms(title))
        {
            if (searchCache.TryGetValue(term, out var cachedValue))
            {
                if (cachedValue.HasValue)
                {
                    return cachedValue.Value;
                }

                continue;
            }

            var response = await httpClient.GetAsync(
                $"search/autocomplete/{Uri.EscapeDataString(term)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                searchCache[term] = null;
                continue;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<SteamGridDbListResponse<SteamGridDbGameMatch>>(
                responseStream,
                JsonOptions,
                cancellationToken);

            var selectedMatch = SelectBestMatch(title, payload?.Data);
            searchCache[term] = selectedMatch?.Id;

            if (selectedMatch is not null)
            {
                return selectedMatch.Id;
            }
        }

        return null;
    }

    private async Task<int> DownloadArtworkSetAsync(
        HttpClient httpClient,
        string gridDirectory,
        string gridId,
        int gameId,
        CancellationToken cancellationToken)
    {
        var updatedFileCount = 0;

        foreach (var slot in ArtworkSlots)
        {
            var assetUrl = await FindTopAssetUrlAsync(httpClient, slot, gameId, cancellationToken);
            if (string.IsNullOrWhiteSpace(assetUrl))
            {
                continue;
            }

            var extension = ResolveFileExtension(assetUrl);
            if (extension is null)
            {
                continue;
            }

            var fileStem = slot.FileStemBuilder(gridId);
            RemoveSlotVariants(gridDirectory, fileStem);

            var targetPath = Path.Combine(gridDirectory, fileStem + extension);
            await DownloadFileAsync(httpClient, assetUrl, targetPath, cancellationToken);
            updatedFileCount++;
        }

        return updatedFileCount;
    }

    private static async Task<string?> FindTopAssetUrlAsync(
        HttpClient httpClient,
        ArtworkSlot slot,
        int gameId,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            string.Format(slot.RequestPath, gameId),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<SteamGridDbListResponse<SteamGridDbAsset>>(
            responseStream,
            JsonOptions,
            cancellationToken);

        return payload?.Data?
            .Select(asset => asset.Url)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
    }

    private static async Task DownloadFileAsync(
        HttpClient httpClient,
        string assetUrl,
        string targetPath,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            assetUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(targetPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    private static SteamGridDbGameMatch? SelectBestMatch(
        string requestedTitle,
        IReadOnlyList<SteamGridDbGameMatch>? matches)
    {
        if (matches is null || matches.Count == 0)
        {
            return null;
        }

        var normalizedRequestedTitle = NormalizeTitle(requestedTitle);

        return matches
            .OrderBy(match => ScoreMatch(normalizedRequestedTitle, NormalizeTitle(match.Name)))
            .ThenBy(match => match.Verified ? 0 : 1)
            .ThenBy(match => Math.Abs((match.Name ?? string.Empty).Length - requestedTitle.Length))
            .FirstOrDefault();
    }

    private static IEnumerable<string> BuildSearchTerms(string title)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddTerm(string? value)
        {
            var trimmedValue = value?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedValue))
            {
                terms.Add(trimmedValue);
            }
        }

        AddTerm(title);

        var withoutBrackets = Regex.Replace(title, @"\s*[\(\[].*?[\)\]]\s*", " ").Trim();
        AddTerm(withoutBrackets);

        var withoutEditionSuffix = Regex.Replace(
            withoutBrackets,
            @"\b(game of the year|goty|ultimate|definitive|complete|deluxe|enhanced|remastered|anniversary|collector'?s|director'?s cut|edition)\b",
            string.Empty,
            RegexOptions.IgnoreCase);
        AddTerm(Regex.Replace(withoutEditionSuffix, @"\s{2,}", " ").Trim(' ', '-', ':'));

        if (withoutEditionSuffix.Contains(" - ", StringComparison.Ordinal))
        {
            AddTerm(withoutEditionSuffix.Split(" - ", 2, StringSplitOptions.TrimEntries)[0]);
        }

        return terms;
    }

    private static string NormalizeTitle(string? value)
    {
        return Regex.Replace(value ?? string.Empty, @"[^a-z0-9]+", string.Empty, RegexOptions.IgnoreCase)
            .ToLowerInvariant();
    }

    private static int ScoreMatch(string requestedTitle, string candidateTitle)
    {
        if (candidateTitle == requestedTitle)
        {
            return 0;
        }

        if (candidateTitle.StartsWith(requestedTitle, StringComparison.Ordinal) ||
            requestedTitle.StartsWith(candidateTitle, StringComparison.Ordinal))
        {
            return 1;
        }

        if (candidateTitle.Contains(requestedTitle, StringComparison.Ordinal) ||
            requestedTitle.Contains(candidateTitle, StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }

    private static string? ResolveFileExtension(string assetUrl)
    {
        if (!Uri.TryCreate(assetUrl, UriKind.Absolute, out var assetUri))
        {
            return null;
        }

        var extension = Path.GetExtension(assetUri.AbsolutePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".ico"
            ? extension
            : null;
    }

    private static void RemoveSlotVariants(string gridDirectory, string fileStem)
    {
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".ico", ".webp" })
        {
            var path = Path.Combine(gridDirectory, fileStem + extension);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed record ArtworkSlot(
        string SlotName,
        string RequestPath,
        Func<string, string> FileStemBuilder);

    private sealed record SteamGridDbListResponse<T>(
        bool Success,
        IReadOnlyList<T> Data);

    private sealed record SteamGridDbGameMatch(
        int Id,
        string Name,
        bool Verified);

    private sealed record SteamGridDbAsset(
        string Url);
}

internal sealed record StoreSyncArtworkTarget(
    string Title,
    uint AppId);

internal sealed record StoreSyncArtworkSummary(
    int UpdatedTitleCount,
    int UpdatedFileCount);
