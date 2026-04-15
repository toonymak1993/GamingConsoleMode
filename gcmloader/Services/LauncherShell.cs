using System;
using System.Threading.Tasks;

namespace gcmloader.Services;

/// <summary>
/// Host hooks for <see cref="LauncherService"/> — keeps WinUI and window-specific behavior out of the service.
/// </summary>
public sealed class LauncherShell
{
    public required Action MakeSelfNonTopmost { get; init; }

    public required Func<IntPtr> GetHostWindowHandle { get; init; }

    public required Func<IntPtr> FindSteamBigPictureWindow { get; init; }

    public required Func<IntPtr, Task> ForcefullyBringToForeground { get; init; }

    public required Func<string, Task> ShowMessageAsync { get; init; }

    public required Action BackToWindows { get; init; }

    public required Func<bool, Task> StartXboxAsync { get; init; }
}
