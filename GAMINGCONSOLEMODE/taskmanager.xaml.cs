// ERSETZE DEN KOMPLETTEN INHALT DEINER taskmanager.xaml.cs HIERMIT

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class taskmanager : Page
    {
        public taskmanager()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => UpdateUI();
        }

        /// <summary>
        /// Lädt alle Einstellungen und aktualisiert die gesamte Benutzeroberfläche.
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                for (int i = 1; i <= 5; i++)
                {
                    LoadSettingsForButton(i); // Lädt alle Einstellungen für jeden Button
                }
                LoadHandheldTouchLauncherSetting();
            }
            catch
            {

            }

            try
            {
                LoadSteamGridDbApiKey();
            }
            catch
            {

            }
        }

        #region SteamGridDB
        private void LoadSteamGridDbApiKey()
        {
            try
            {
                SteamGridDbApiKeyBox.Text = AppSettings.Load<string>("steamgriddb_api_key");
            }
            catch { /* Einstellung existiert nicht, ignoriere. */ }
        }

        private async void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("steamgriddb_api_key", SteamGridDbApiKeyBox.Text.Trim());
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Gespeichert",
                Content = "API Key wurde erfolgreich gespeichert!",
                CloseButtonText = "Ok"
            };
            await dialog.ShowAsync();
        }
        #endregion

        #region Launcher Konfiguration (NEU & VERBESSERT)

        // --- HILFSMETHODEN, DIE DEN CODE STARK VEREINFACHEN ---

        /// <summary>
        /// Lädt die Einstellungen für einen bestimmten Button-Slot und zeigt sie in der UI an.
        /// </summary>
        private void LoadSettingsForButton(int index)
        {
            // Finde die richtigen UI-Elemente basierend auf dem Index
            var imageControl = this.FindName($"Image{index}") as Image;
            var argsBox = this.FindName($"Args{index}") as TextBox;
            if (imageControl == null || argsBox == null) return;

            try
            {
                // Bild laden
                string imagePath = AppSettings.Load<string>($"button{index}image");
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    imageControl.Source = new BitmapImage(new Uri(imagePath));
                }

                // Parameter laden
                argsBox.Text = AppSettings.Load<string>($"button{index}args");
            }
            catch { /* Ignoriere Fehler, falls eine Einstellung fehlt */ }
        }

        /// <summary>
        /// Speichert alle Einstellungen für einen bestimmten Button-Slot.
        /// </summary>
        private void SaveSettingsForButton(int index)
        {
            var argsBox = this.FindName($"Args{index}") as TextBox;
            if (argsBox == null) return;

            try
            {
                AppSettings.Save($"button{index}args", argsBox.Text);
                AppSettings.Save($"button{index}", true); // Markiere den Slot als "aktiv"
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Fehler beim Speichern für Button {index}: {ex.Message}");
            }
        }

        // --- EVENT HANDLER (jetzt viel kürzer) ---

        private async void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("SelectImage", "").Replace("_Click", ""));
            var imageControl = this.FindName($"Image{index}") as Image;

            var file = await PickFileAsync(new[] { ".png", ".jpg", ".jpeg" });
            if (file != null && imageControl != null)
            {
                imageControl.Source = new BitmapImage(new Uri(file.Path));
                AppSettings.Save($"button{index}image", file.Path);
                SaveSettingsForButton(index);
            }
        }

        private async void SelectLink_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("SelectLink", "").Replace("_Click", ""));

            var file = await PickFileAsync(new[] { ".exe" });
            if (file != null)
            {
                AppSettings.Save($"button{index}link", file.Path);
                SaveSettingsForButton(index);
            }
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("Test", "").Replace("_Click", ""));
            try
            {
                string exePath = AppSettings.Load<string>($"button{index}link");
                string arguments = (this.FindName($"Args{index}") as TextBox)?.Text ?? "";

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo(exePath) { Arguments = arguments, UseShellExecute = true });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[TEST] Fehler: {ex.Message}"); }
        }

        private void Args_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Finde heraus, welche Textbox geändert wurde, indem wir die Zahl aus dem Namen lesen.
            var textBox = sender as TextBox;
            int index = int.Parse(Regex.Match(textBox.Name, @"\d+").Value);

            // Speichere den neuen Text in der Einstellungsdatei unter dem passenden Schlüssel (z.B. "button1args").
            AppSettings.Save($"button{index}args", textBox.Text);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("Reset", "").Replace("_Click", ""));
            var imageControl = this.FindName($"Image{index}") as Image;
            var argsBox = this.FindName($"Args{index}") as TextBox;

            if (imageControl != null) imageControl.Source = null;
            if (argsBox != null) argsBox.Text = "";

            AppSettings.Delete($"button{index}");
            AppSettings.Delete($"button{index}link");
            AppSettings.Delete($"button{index}image");
            AppSettings.Delete($"button{index}args");
        }

        // --- ALLGEMEINE HILFSMETHODE FÜR DATEIAUSWAHL ---

        private async Task<Windows.Storage.StorageFile> PickFileAsync(string[] fileTypes)
        {
            var picker = new FileOpenPicker();
            foreach (var type in fileTypes) picker.FileTypeFilter.Add(type);

            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            return await picker.PickSingleFileAsync();
        }

        #endregion

        #region Handheld Toggle
        private void LoadHandheldTouchLauncherSetting()
        {
            try { handheldtouchlauncher.IsOn = AppSettings.Load<bool>("handheldtouchlauncher"); }
            catch { handheldtouchlauncher.IsOn = false; }
        }
        private void handheldtouchlauncher_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) AppSettings.Save("handheldtouchlauncher", toggle.IsOn);
        }
        #endregion
    }
}