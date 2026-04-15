using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace gcmloader.Services;

/// <summary>
/// Optional shared helpers for directional / Escape keyboard routing (hub ↔ gcmloader parity).
/// Extend when consolidating custom FocusArea-style logic.
/// </summary>
public static class FocusNavigationHelper
{
    /// <summary>Returns true if the key is Escape.</summary>
    public static bool IsCancelKey(KeyRoutedEventArgs e)
    {
        return e.Key == VirtualKey.Escape;
    }
}
