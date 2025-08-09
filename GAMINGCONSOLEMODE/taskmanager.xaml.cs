// ERSETZE DEN KOMPLETTEN INHALT DEINER taskmanager.xaml.cs HIERMIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

// Stelle sicher, dass der Namespace zu deinem Projekt passt
namespace GAMINGCONSOLEMODE
{
    public sealed partial class taskmanager : Page
    {
        public taskmanager()
        {
            this.InitializeComponent();
            // Die UI wird jetzt beim Laden der Seite aktualisiert, was robuster ist
            this.Loaded += (s, e) => UpdateUI();
        }

        #region update ui

        /// <summary>
        /// Lädt alle Einstellungen und aktualisiert die gesamte Benutzeroberfläche.
        /// </summary>
        private void UpdateUI()
        {
            // Lädt die Bilder für die 5 Launcher-Buttons
            LoadImageIfExists(Image1, "button1");
            LoadImageIfExists(Image2, "button2");
            LoadImageIfExists(Image3, "button3");
            LoadImageIfExists(Image4, "button4");
            LoadImageIfExists(Image5, "button5");

            // Lädt die Einstellung für den Handheld-Toggle
            LoadHandheldTouchLauncherSetting();

            // NEU: Lädt die Einstellung für den SteamGridDB API Key
            LoadSteamGridDbApiKey();
        }

        #endregion update ui

        // --- NEUE METHODEN FÜR STEAMGRIDDB ---
        #region SteamGridDB

        /// <summary>
        /// Lädt den gespeicherten SteamGridDB API-Schlüssel und zeigt ihn im Textfeld an.
        /// </summary>
        private void LoadSteamGridDbApiKey()
        {
            try
            {
                string apiKey = AppSettings.Load<string>("steamgriddb_api_key");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    SteamGridDbApiKeyBox.Text = apiKey;
                }
            }
            catch
            {
                // Einstellung existiert noch nicht, das ist in Ordnung.
            }
        }

        /// <summary>
        /// Speichert den eingegebenen API-Schlüssel, wenn der Button geklickt wird.
        /// </summary>
        private async void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = SteamGridDbApiKeyBox.Text.Trim();

            AppSettings.Save("steamgriddb_api_key", apiKey);

            // Zeigt dem Benutzer eine Erfolgsmeldung.
            ContentDialog confirmationDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Success",
                Content = "API Key saved successfully!",
                CloseButtonText = "Ok"
            };

            await confirmationDialog.ShowAsync();
        }

        #endregion SteamGridDB


        #region launcher 

        private void LoadHandheldTouchLauncherSetting()
        {
            try
            {
                bool isOn = AppSettings.Load<bool>("handheldtouchlauncher");
                handheldtouchlauncher.IsOn = isOn;
            }
            catch
            {
                handheldtouchlauncher.IsOn = false; // Fallback
            }
        }

        private void handheldtouchlauncher_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                AppSettings.Save("handheldtouchlauncher", toggle.IsOn);
            }
        }

        private void LoadImageIfExists(Image imageControl, string keyName)
        {
            try
            {
                string imagePath = AppSettings.Load<string>($"{keyName}image");
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    var bitmap = new BitmapImage(new Uri(imagePath));
                    imageControl.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Could not load image for {keyName}: {ex.Message}");
            }
        }

        private async Task SetImageAsync(Image imageControl, string keyName)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream);
                    imageControl.Source = bitmap;

                    AppSettings.Save($"{keyName}image", file.Path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to set image: {ex.Message}");
                }
            }
        }

        private async Task SetLinkAsync(string keyName)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".exe");
            picker.SuggestedStartLocation = PickerLocationId.Desktop;

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                AppSettings.Save($"{keyName}link", file.Path);
                AppSettings.Save($"{keyName}", true);
            }
        }

        private async void SelectImage1_Click(object sender, RoutedEventArgs e) => await SetImageAsync(Image1, "button1");
        private async void SelectImage2_Click(object sender, RoutedEventArgs e) => await SetImageAsync(Image2, "button2");
        private async void SelectImage3_Click(object sender, RoutedEventArgs e) => await SetImageAsync(Image3, "button3");
        private async void SelectImage4_Click(object sender, RoutedEventArgs e) => await SetImageAsync(Image4, "button4");
        private async void SelectImage5_Click(object sender, RoutedEventArgs e) => await SetImageAsync(Image5, "button5");

        private async void SelectLink1_Click(object sender, RoutedEventArgs e) => await SetLinkAsync("button1");
        private async void SelectLink2_Click(object sender, RoutedEventArgs e) => await SetLinkAsync("button2");
        private async void SelectLink3_Click(object sender, RoutedEventArgs e) => await SetLinkAsync("button3");
        private async void SelectLink4_Click(object sender, RoutedEventArgs e) => await SetLinkAsync("button4");
        private async void SelectLink5_Click(object sender, RoutedEventArgs e) => await SetLinkAsync("button5");

        private void Reset1_Click(object sender, RoutedEventArgs e) => ResetLauncher("button1", Image1);
        private void Reset2_Click(object sender, RoutedEventArgs e) => ResetLauncher("button2", Image2);
        private void Reset3_Click(object sender, RoutedEventArgs e) => ResetLauncher("button3", Image3);
        private void Reset4_Click(object sender, RoutedEventArgs e) => ResetLauncher("button4", Image4);
        private void Reset5_Click(object sender, RoutedEventArgs e) => ResetLauncher("button5", Image5);

        private void ResetLauncher(string keyName, Image imageControl)
        {
            try
            {
                AppSettings.Save($"{keyName}", false);
                AppSettings.Save($"{keyName}link", string.Empty);
                AppSettings.Save($"{keyName}image", string.Empty);
                imageControl.Source = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to reset {keyName}: {ex.Message}");
            }
        }

        private void Test1_Click(object sender, RoutedEventArgs e) => LaunchExeFromSetting("button1link");
        private void Test2_Click(object sender, RoutedEventArgs e) => LaunchExeFromSetting("button2link");
        private void Test3_Click(object sender, RoutedEventArgs e) => LaunchExeFromSetting("button3link");
        private void Test4_Click(object sender, RoutedEventArgs e) => LaunchExeFromSetting("button4link");
        private void Test5_Click(object sender, RoutedEventArgs e) => LaunchExeFromSetting("button5link");

        private void LaunchExeFromSetting(string settingKey)
        {
            try
            {
                string exePath = AppSettings.Load<string>(settingKey);
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Debug.WriteLine($"[TEST] No valid EXE path found in {settingKey}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to launch {settingKey}: {ex.Message}");
            }
        }
        #endregion launcher
    }
}