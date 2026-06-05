namespace SteamLoader.App.Infrastructure.Steam;

public sealed record SteamDevToolsEvaluationResult(
    bool Success,
    object? Value,
    string? ErrorMessage);
