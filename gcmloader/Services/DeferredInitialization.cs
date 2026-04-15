using System;
using Microsoft.UI.Dispatching;

namespace gcmloader.Services;

/// <summary>
/// Staggers non-critical work after first paint via the UI queue (low priority).
/// </summary>
public static class DeferredInitialization
{
    public static void RunAfterFirstPaint(DispatcherQueue dispatcher, Action work)
    {
        if (dispatcher == null || work == null) return;

        dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeferredInit] {ex.Message}");
            }
        });
    }
}
