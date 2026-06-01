using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace gcmloader
{
    public class ShortcutDisplayItem
    {
        public string KeysDisplay { get; set; }
        public string ActionName { get; set; }
    }

    public sealed partial class BlankWindow1 : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const long WS_POPUP = 0x80000000L;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        public BlankWindow1(List<ShortcutDisplayItem> shortcuts)
        {
            this.InitializeComponent();
            ShortcutItemsControl.ItemsSource = shortcuts;

            // Lade das Hintergrundbild
            LoadWallpaper();

            // Setze das Fenster in den Vordergrund
            SetupTrueBorderlessTopmostWindow();
        }

        private void LoadWallpaper()
        {
            try
            {
                string rawPath = "";
                bool useGcmWallpaper = false;

                try { useGcmWallpaper = AppSettings.Load<bool>("gcmwallpaper"); } catch { }

                if (useGcmWallpaper)
                {
                    try { rawPath = AppSettings.Load<string>("gcmwallpaperpath"); } catch { }
                }

                // Fallback auf Windows Wallpaper
                if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath.Trim('"').Trim()))
                {
                    rawPath = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "") as string;
                }

                if (!string.IsNullOrEmpty(rawPath))
                {
                    string cleanPath = rawPath.Trim('"').Trim();
                    if (File.Exists(cleanPath))
                    {
                        // Bild setzen
                        WallpaperImage.Source = new BitmapImage(new Uri(cleanPath, UriKind.Absolute));
                    }
                }
            }
            catch { /* Ignorieren, XAML hat bereits einen Fallback-Hintergrund */ }
        }

        private void SetupTrueBorderlessTopmostWindow()
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            SetWindowLongPtr(hwnd, GWL_STYLE, (IntPtr)WS_POPUP);
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)0);

            int screenWidth = GetSystemMetrics(0);
            int screenHeight = GetSystemMetrics(1);

            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, screenWidth, screenHeight, 0x0040);

            this.Activate();
        }

        private void RootBackground_Loaded(object sender, RoutedEventArgs e)
        {
            var sb = new Storyboard();

            var fadeAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(200) };
            Storyboard.SetTarget(fadeAnim, RootBackground);
            Storyboard.SetTargetProperty(fadeAnim, "Opacity");

            var scaleXAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            var scaleYAnim = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(scaleXAnim, PopupTransform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            Storyboard.SetTarget(scaleYAnim, PopupTransform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

            var slideAnim = new DoubleAnimation { To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideAnim, PopupTransform);
            Storyboard.SetTargetProperty(slideAnim, "TranslateY");

            sb.Children.Add(fadeAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);
            sb.Children.Add(slideAnim);
            sb.Begin();
        }

        public void CloseAnimated(Action onCompleted = null)
        {
            var sb = new Storyboard();

            var fadeAnim = new DoubleAnimation { To = 0.0, Duration = TimeSpan.FromMilliseconds(150) };
            Storyboard.SetTarget(fadeAnim, RootBackground);
            Storyboard.SetTargetProperty(fadeAnim, "Opacity");

            var scaleXAnim = new DoubleAnimation { To = 0.9, Duration = TimeSpan.FromMilliseconds(150) };
            var scaleYAnim = new DoubleAnimation { To = 0.9, Duration = TimeSpan.FromMilliseconds(150) };
            Storyboard.SetTarget(scaleXAnim, PopupTransform);
            Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
            Storyboard.SetTarget(scaleYAnim, PopupTransform);
            Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

            sb.Children.Add(fadeAnim);
            sb.Children.Add(scaleXAnim);
            sb.Children.Add(scaleYAnim);

            sb.Completed += (s, e) =>
            {
                this.Close();
                onCompleted?.Invoke();
            };
            sb.Begin();
        }
    }
}
