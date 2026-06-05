using System.Text.Json;

namespace SteamLoader.App.Infrastructure.Steam;

public sealed class SteamFrontendComponentService
{
    private readonly SteamDevToolsClient _devToolsClient;

    public SteamFrontendComponentService(SteamDevToolsClient devToolsClient)
    {
        _devToolsClient = devToolsClient;
    }

    public async Task<object> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var target = await _devToolsClient.GetSharedJsContextTargetAsync(cancellationToken);
        if (target is null)
        {
            return new
            {
                runtimeReady = false,
                moduleCount = 0,
                availableCount = 0,
                totalCount = 0,
                components = Array.Empty<object>(),
                errors = new[] { "Steam SharedJSContext is not available." },
            };
        }

        var result = await _devToolsClient.EvaluateAsync(
            target.WebSocketDebuggerUrl,
            "window.SteamToolsFrontendRegistry?.refresh?.() ?? { runtimeReady: false, errors: ['Tools for Steam frontend registry is not injected.'] }",
            cancellationToken);

        if (!result.Success)
        {
            return new
            {
                runtimeReady = false,
                moduleCount = 0,
                availableCount = 0,
                totalCount = 0,
                components = Array.Empty<object>(),
                errors = new[] { result.ErrorMessage ?? "Steam frontend registry could not be read." },
            };
        }

        return result.Value is JsonElement jsonElement
            ? jsonElement
            : result.Value ?? new
            {
                runtimeReady = false,
                moduleCount = 0,
                availableCount = 0,
                totalCount = 0,
                components = Array.Empty<object>(),
                errors = new[] { "Steam frontend registry returned no data." },
            };
    }
}
