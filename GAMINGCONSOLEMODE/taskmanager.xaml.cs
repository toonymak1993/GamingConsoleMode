
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Button = Microsoft.UI.Xaml.Controls.Button;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;


namespace GAMINGCONSOLEMODE
{
    public sealed partial class taskmanager : Microsoft.UI.Xaml.Controls.Page
    {
        public taskmanager()
        {
            this.InitializeComponent();
            this.Loaded += (s, e) => UpdateUI();
        }

        /// <summary>
        /// Loads all settings and updates the entire user interface.
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                for (int i = 1; i <= 5; i++)
                {
                    LoadSettingsForButton(i); // Loads all settings for each button
                }
                LoadHandheldTouchLauncherSetting();
            }
            catch
            {
                // Ignore potential errors during UI update
            }

            try
            {
                LoadSteamGridDbApiKey();
            }
            catch
            {
                // Ignore if API key setting doesn't exist
            }

            LoadToggleSettings();
        }

        #region SteamGridDB
        private void LoadSteamGridDbApiKey()
        {
            try
            {
                SteamGridDbApiKeyBox.Text = AppSettings.Load<string>("steamgriddb_api_key");
            }
            catch { /* Setting does not exist, ignore. */ }
        }

        private async void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("steamgriddb_api_key", SteamGridDbApiKeyBox.Text.Trim());
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Saved",
                Content = "API key successfully saved!",
                CloseButtonText = "Ok"
            };
            await dialog.ShowAsync();
        }
        #endregion

        #region Launcher Configuration (NEW & IMPROVED)

        // --- HELPER METHODS THAT GREATLY SIMPLIFY THE CODE ---

        /// <summary>
        /// Loads the settings for a specific button slot and displays them in the UI.
        /// </summary>
        private void LoadSettingsForButton(int index)
        {
            // Find the correct UI elements based on the index
            var imageControl = this.FindName($"Image{index}") as Image;
            var argsBox = this.FindName($"Args{index}") as Microsoft.UI.Xaml.Controls.TextBox;
            var workDirBox = this.FindName($"WorkDir{index}") as Microsoft.UI.Xaml.Controls.TextBox; // *** ADDED ***
            if (imageControl == null || argsBox == null || workDirBox == null) return; // *** MODIFIED ***

            try
            {
                // Load image
                string imagePath = AppSettings.Load<string>($"button{index}image");
                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                {
                    imageControl.Source = new BitmapImage(new Uri(imagePath));
                }

                // Load parameters
                argsBox.Text = AppSettings.Load<string>($"button{index}args");

                // *** ADDED: Load Working Directory ***
                workDirBox.Text = AppSettings.Load<string>($"button{index}workdir");
            }
            catch { /* Ignore errors if a setting is missing */ }
        }

        /// <summary>
        /// Saves all settings for a specific button slot.
        /// </summary>
        private void SaveSettingsForButton(int index)
        {
            var argsBox = this.FindName($"Args{index}") as Microsoft.UI.Xaml.Controls.TextBox;
            var workDirBox = this.FindName($"WorkDir{index}") as Microsoft.UI.Xaml.Controls.TextBox; // *** ADDED ***
            if (argsBox == null || workDirBox == null) return; // *** MODIFIED ***

            try
            {
                AppSettings.Save($"button{index}args", argsBox.Text);
                AppSettings.Save($"button{index}workdir", workDirBox.Text); // *** ADDED ***
                AppSettings.Save($"button{index}", true); // Mark the slot as "active"
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error saving for Button {index}: {ex.Message}");
            }
        }

        // --- EVENT HANDLERS (now much shorter) ---

        private async void SelectImage_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("SelectImage", ""));
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
            int index = int.Parse((sender as Button).Name.Replace("SelectLink", ""));
            var workDirBox = this.FindName($"WorkDir{index}") as Microsoft.UI.Xaml.Controls.TextBox; // *** ADDED ***

            var file = await PickFileAsync(new[] { ".exe" });
            if (file != null)
            {
                AppSettings.Save($"button{index}link", file.Path);

                // *** ADDED: Automatically set and save the working directory ***
                if (workDirBox != null)
                {
                    string directory = Path.GetDirectoryName(file.Path);
                    workDirBox.Text = directory; // Set text in UI
                    // The TextChanged event will handle saving
                }

                SaveSettingsForButton(index);
            }
        }

        private void Test_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("Test", ""));
            try
            {
                string exePath = AppSettings.Load<string>($"button{index}link");
                string arguments = (this.FindName($"Args{index}") as Microsoft.UI.Xaml.Controls.TextBox)?.Text ?? "";
                string workDir = (this.FindName($"WorkDir{index}") as Microsoft.UI.Xaml.Controls.TextBox)?.Text ?? ""; // *** ADDED ***

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    // *** MODIFIED: Use ProcessStartInfo to include WorkingDirectory ***
                    var startInfo = new ProcessStartInfo(exePath)
                    {
                        Arguments = arguments,
                        UseShellExecute = true
                    };

                    if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                    {
                        startInfo.WorkingDirectory = workDir;
                    }

                    Process.Start(startInfo);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[TEST] Error: {ex.Message}"); }
        }

        private void Args_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Find out which textbox was changed by reading the number from its name.
            var textBox = sender as Microsoft.UI.Xaml.Controls.TextBox;
            int index = int.Parse(Regex.Match(textBox.Name, @"\d+").Value);

            // Save the new text to the settings file under the correct key (e.g., "button1args").
            AppSettings.Save($"button{index}args", textBox.Text);
        }

        // *** NEW METHOD ***
        /// <summary>
        /// Saves the working directory path when the user types in the TextBox.
        /// </summary>
        private void WorkDir_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Find out which textbox was changed by reading the number from its name.
            var textBox = sender as TextBox;

            int index = int.Parse(Regex.Match(textBox.Name, @"\d+").Value);

            // Save the new text to the settings file under the correct key (e.g., "button1workdir").
            AppSettings.Save($"button{index}workdir", textBox.Text);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            int index = int.Parse((sender as Button).Name.Replace("Reset", ""));
            var imageControl = this.FindName($"Image{index}") as Image;
            var argsBox = this.FindName($"Args{index}") as TextBox;
            var workDirBox = this.FindName($"WorkDir{index}") as TextBox; // *** ADDED ***

            if (imageControl != null) imageControl.Source = null;
            if (argsBox != null) argsBox.Text = "";
            if (workDirBox != null) workDirBox.Text = ""; // *** ADDED ***

            AppSettings.Delete($"button{index}");
            AppSettings.Delete($"button{index}link");
            AppSettings.Delete($"button{index}image");
            AppSettings.Delete($"button{index}args");
            AppSettings.Delete($"button{index}workdir"); // *** ADDED ***
        }

        // --- GENERAL HELPER METHOD FOR FILE SELECTION ---

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

        #region settingsframe
        private void LoadToggleSettings()
        {
            // 1. Taskbar Setting laden (Standard: Aus / false)
            try
            {
                TaskbarToggle.IsOn = AppSettings.Load<bool>("enable_taskbar");
            }
            catch
            {
                TaskbarToggle.IsOn = false;
                AppSettings.Save("enable_taskbar", false);
            }

            // 2. Discord Setting laden (Standard: An / true, damit der Button erstmal da ist)
            try
            {
                DiscordToggle.IsOn = AppSettings.Load<bool>("show_discord");
            }
            catch
            {
                DiscordToggle.IsOn = true;
                AppSettings.Save("show_discord", true);
            }
        }

        // --- EVENT HANDLER FÜR DIE SCHALTER ---

        private void Taskbar_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                AppSettings.Save("enable_taskbar", toggle.IsOn);
                Debug.WriteLine($"[Settings] 'enable_taskbar' gespeichert: {toggle.IsOn}");
            }
        }

        private void Discord_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle)
            {
                AppSettings.Save("show_discord", toggle.IsOn);
                Debug.WriteLine($"[Settings] 'show_discord' gespeichert: {toggle.IsOn}");
            }
        }

        #endregion

        private void Startmenu_Toggled(object sender, RoutedEventArgs e)
        {
            // This event is triggered when the "Enable Startmenu" toggle is changed.
            if (sender is ToggleSwitch toggle)
            {
                bool isEnabled = toggle.IsOn;
                AppSettings.Save("enable_startmenu", isEnabled);
                Debug.WriteLine($"Setting 'enable_startmenu' saved: {isEnabled}");
            }
        }


      
    }
}