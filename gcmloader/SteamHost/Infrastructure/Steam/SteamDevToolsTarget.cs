namespace SteamLoader.App.Infrastructure.Steam;

public sealed record SteamDevToolsTarget(
    string Id,
    string Title,
    string Type,
    string Url,
    string WebSocketDebuggerUrl);
