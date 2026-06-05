using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SteamLoader.App.Infrastructure.Steam;

public sealed class SteamDevToolsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Uri _debugEndpoint;

    public SteamDevToolsClient(HttpClient httpClient, Uri debugEndpoint)
    {
        _httpClient = httpClient;
        _debugEndpoint = debugEndpoint;
    }

    public async Task<SteamDevToolsTarget?> GetSharedJsContextTargetAsync(CancellationToken cancellationToken)
    {
        var targets = await GetTargetsAsync(cancellationToken);
        return targets.FirstOrDefault(target =>
            string.Equals(target.Title, "SharedJSContext", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SteamDevToolsTarget?> GetQuickAccessTargetAsync(CancellationToken cancellationToken)
    {
        var targets = await GetTargetsAsync(cancellationToken);
        return targets.FirstOrDefault(target => target.Title.StartsWith("QuickAccess", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SteamDevToolsTarget?> GetBigPictureTargetAsync(CancellationToken cancellationToken)
    {
        var targets = await GetTargetsAsync(cancellationToken);
        return targets.FirstOrDefault(target =>
            target.Title.Contains("Big-Picture", StringComparison.OrdinalIgnoreCase) ||
            target.Url.Contains("steamloopback.host/index.html", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<SteamDevToolsTarget>> GetThemeSurfaceTargetsAsync(CancellationToken cancellationToken)
    {
        var targets = await GetTargetsAsync(cancellationToken);
        return targets
            .Where(target =>
                string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(target.Title, "SharedJSContext", StringComparison.OrdinalIgnoreCase) ||
                 target.Title.Contains("Big-Picture", StringComparison.OrdinalIgnoreCase) ||
                 target.Url.Contains("steamloopback.host", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public async Task<IReadOnlyList<SteamDevToolsTarget>> GetTargetsAsync(CancellationToken cancellationToken)
    {
        var targetsUri = new Uri(_debugEndpoint, "/json/list");
        await using var stream = await _httpClient.GetStreamAsync(targetsUri, cancellationToken);
        var targets = await JsonSerializer.DeserializeAsync<List<SteamDevToolsTarget>>(stream, JsonOptions, cancellationToken);
        return targets ?? [];
    }

    public async Task<SteamDevToolsEvaluationResult> EvaluateAsync(
        string webSocketDebuggerUrl,
        string expression,
        CancellationToken cancellationToken)
    {
        using var webSocket = new ClientWebSocket();
        await webSocket.ConnectAsync(new Uri(webSocketDebuggerUrl), cancellationToken);

        var payload = JsonSerializer.Serialize(
            new DevToolsCommand(
                1,
                "Runtime.evaluate",
                new DevToolsCommandParameters(expression, true, true)),
            JsonOptions);

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        await webSocket.SendAsync(
            payloadBytes,
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        var responseText = await ReceiveMessageAsync(webSocket, cancellationToken);
        var response = JsonSerializer.Deserialize<DevToolsResponse>(responseText, JsonOptions);

        if (response?.Error is not null)
        {
            return new SteamDevToolsEvaluationResult(false, null, response.Error.Message);
        }

        if (response?.Result?.ExceptionDetails is not null)
        {
            return new SteamDevToolsEvaluationResult(
                false,
                null,
                response.Result.ExceptionDetails.Text ?? "JavaScript evaluation failed.");
        }

        return new SteamDevToolsEvaluationResult(
            true,
            response?.Result?.Result?.Value,
            null);
    }

    private static async Task<string> ReceiveMessageAsync(ClientWebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var memory = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            memory.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private sealed record DevToolsCommand(int Id, string Method, DevToolsCommandParameters Params);

    private sealed record DevToolsCommandParameters(
        string Expression,
        bool ReturnByValue,
        bool AwaitPromise);

    private sealed record DevToolsResponse(
        int Id,
        DevToolsResponsePayload? Result,
        DevToolsError? Error);

    private sealed record DevToolsResponsePayload(
        DevToolsRemoteObject? Result,
        DevToolsExceptionDetails? ExceptionDetails);

    private sealed record DevToolsRemoteObject(
        string? Type,
        object? Value,
        string? Description);

    private sealed record DevToolsExceptionDetails(
        string? Text);

    private sealed record DevToolsError(
        int Code,
        string Message);
}
