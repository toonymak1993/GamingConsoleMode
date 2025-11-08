using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Windows.Forms;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class launcher : Page
    {

        public launcher()
        {
            this.InitializeComponent();
            InitializeUI();
        }

        #region Methods

        // Handles the button click to toggle the SplitView pane.
        private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        /// <summary>
        /// Sets up the initial state of the UI based on saved settings.
        /// </summary>
        private void InitializeUI()
        {
            // We'll try to load the saved paths. If it fails (e.g., first launch),
            // the empty catch block will just let it continue gracefully.
            try
            {
                #region Launcher Paths
                string steamlauncherpath = AppSettings.Load<string>("steamlauncherpath");
                textbox_steam_path.Text = steamlauncherpath;

                string playnitelauncherpath = AppSettings.Load<string>("playnitelauncherpath");
                textbox_playnite_path.Text = playnitelauncherpath;

                string customlauncherpath = AppSettings.Load<string>("customlauncherpath");
                textbox_custom_path.Text = customlauncherpath;
                #endregion
            }
            catch
            {
                // No settings found? No problem. We'll just start with empty text boxes.
            }

            // Figure out which launcher was last selected and set the right toggle switch.
            string launcher = AppSettings.Load<string>("launcher");
            switch (launcher)
            {
                case "steam":
                    use_steam_bp.IsOn = true;
                    use_playnite.IsOn = false;
                    use_custom.IsOn = false;
                    use_xbox.IsOn = false;
                    break;

                case "playnite":
                    use_playnite.IsOn = true;
                    use_steam_bp.IsOn = false;
                    use_custom.IsOn = false;
                    use_xbox.IsOn = false;
                    break;

                case "custom":
                    use_custom.IsOn = true;
                    use_playnite.IsOn = false;
                    use_steam_bp.IsOn = false;
                    use_xbox.IsOn = false;
                    break;

                case "xbox":
                    use_custom.IsOn = false;
                    use_playnite.IsOn = false;
                    use_steam_bp.IsOn = false;
                    use_xbox.IsOn = true;
                    break;

                default:
                    // If we find an invalid or no setting, let's just default to Steam.
                    Console.WriteLine("Invalid launcher setting. Defaulting to Steam.");
                    launcher = "steam";
                    AppSettings.Save("launcher", launcher);

                    use_steam_bp.IsOn = true;
                    use_playnite.IsOn = false;
                    use_custom.IsOn = false;
                    use_xbox.IsOn = false;
                    break;
            }
        }

        /// <summary>
        /// Opens a file dialog to let the user pick an executable file.
        /// </summary>
        /// <returns>The full path to the selected .exe, or "none" if canceled.</returns>
        private string GetExePath()
        {
            string file = FilePicker.ShowDialog(
                "C:\\",                         // Where to start looking.
                new string[] { "exe" },         // We only want .exe files.
                "Executable Files",             // The description for the filter.
                "Select an Executable File"     // The title of the dialog window.
            );

            if (!string.IsNullOrEmpty(file))
            {
                // The user picked a file. Let's return the path.
                return file;
            }
            else
            {
                // The user canceled the dialog.
                return "none";
            }
        }

        /// <summary>
        /// Checks if at least one launcher toggle is active. If not, it shows a popup
        /// and resets the choice to Steam as a fallback.
        /// </summary>
        private async Task CheckLauncherActivatedAsync()
        {
            if (use_steam_bp.IsOn == false & use_playnite.IsOn == false & use_custom.IsOn == false & use_xbox.IsOn == false)
            {
                // Whoops, nothing is selected. We need to tell the user and fix it.
                var dialog = new ContentDialog
                {
                    Title = "Information",
                    Content = "Please select at least one launcher. The default launcher will now be set.",
                    CloseButtonText = "OK",
                    // This is crucial - it connects the dialog to our app's main window.
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();

                // Set Steam as the default and save it.
                AppSettings.Save("launcher", "steam");

                // Refresh the UI to reflect the change.
                InitializeUI();
            }
        }

        #endregion Methods

        #region Events

        #region Steam Events
        private void textbox_steam_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("steamlauncherpath", textbox_steam_path.Text);
            // Refresh the UI to make sure everything's in sync.
            InitializeUI();
        }

        private async void use_steam_bp_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_steam_bp.IsOn == true)
            {
                AppSettings.Save("launcher", "steam");
                InitializeUI();
                // Show the relevant settings panel.
                SteamPanel.Visibility = Visibility.Visible;
                PlaynitePanel.Visibility = Visibility.Collapsed;
                CustomPanel.Visibility = Visibility.Collapsed;
                XboxPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Make sure at least one launcher is still active.
                await CheckLauncherActivatedAsync();
                SteamPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void pichsteampath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();

            if (exepath == "none")
            {
                return; // User canceled.
            }

            string expectedFileName = "steam.exe";
            string selectedFile = System.IO.Path.GetFileName(exepath);

            // Let's make sure they picked the right file.
            if (selectedFile.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                // Looks good. Save the path and update the UI.
                AppSettings.Save("steamlauncherpath", exepath);
                InitializeUI();
            }
            else
            {
                MessageBox.Show($"Please select the correct file: {expectedFileName}", "Invalid File");
            }
        }
        #endregion

        #region Playnite Events

        private void pichplaynitepath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();

            if (exepath == "none")
            {
                return; // User canceled.
            }

            string expectedFileName = "Playnite.FullscreenApp.exe";
            string selectedFile = System.IO.Path.GetFileName(exepath);

            if (selectedFile.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                // Correct file. Save and refresh.
                AppSettings.Save("playnitelauncherpath", exepath);
                InitializeUI();
            }
            else
            {
                MessageBox.Show($"Please select the correct file: {expectedFileName}", "Invalid File");
            }
        }

        private async void use_playnite_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_playnite.IsOn == true)
            {
                AppSettings.Save("launcher", "playnite");
                InitializeUI();
                // Show the Playnite panel and hide the others.
                SteamPanel.Visibility = Visibility.Collapsed;
                PlaynitePanel.Visibility = Visibility.Visible;
                CustomPanel.Visibility = Visibility.Collapsed;
                XboxPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                await CheckLauncherActivatedAsync();
                PlaynitePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void textbox_playnite_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("playnitelauncherpath", textbox_playnite_path.Text);
            InitializeUI();
        }
        #endregion

        #region Xbox Events
        private async void use_xbox_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_xbox.IsOn == true)
            {
                AppSettings.Save("launcher", "xbox");
                InitializeUI();
                SteamPanel.Visibility = Visibility.Collapsed;
                PlaynitePanel.Visibility = Visibility.Collapsed;
                CustomPanel.Visibility = Visibility.Collapsed;
                XboxPanel.Visibility = Visibility.Visible;
            }
            else
            {
                await CheckLauncherActivatedAsync();
                XboxPanel.Visibility = Visibility.Collapsed;
            }
        }
        #endregion

        #region Custom Launcher Events

        private void pichcustompath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();

            if (exepath != "none")
            {
                // For a custom launcher, any .exe is fine.
                AppSettings.Save("customlauncherpath", exepath);
                InitializeUI();
            }
        }

        private async void use_custom_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_custom.IsOn == true)
            {
                AppSettings.Save("launcher", "custom");
                InitializeUI();
                SteamPanel.Visibility = Visibility.Collapsed;
                PlaynitePanel.Visibility = Visibility.Collapsed;
                CustomPanel.Visibility = Visibility.Visible;
                XboxPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                await CheckLauncherActivatedAsync();
                CustomPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void textbox_custom_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("customlauncherpath", textbox_custom_path.Text);
            InitializeUI();
        }

        #endregion

        #endregion Events
    }

    /// <summary>
    /// This static class wraps the native Win32 API for showing a file open dialog.
    /// It's a bit old-school, but it gets the job done reliably.
    /// </summary>
    public static class FilePicker
    {
        [System.Runtime.InteropServices.DllImport("comdlg32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        // Here's an example of how you'd use this:
        // string filename = FilePicker.ShowDialog("C:\\", new string[] { "png", "jpeg" }, "Image Files", "Select an Image...");
        public static string ShowDialog(string startingDirectory, string[] filters, string filterName, string dialogTitle)
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = System.Runtime.InteropServices.Marshal.SizeOf(ofn);

            // Building the filter string in the format the API expects.
            ofn.lpstrFilter = filterName;
            foreach (string filter in filters)
            {
                ofn.lpstrFilter += $"\0*.{filter}";
            }

            ofn.lpstrFile = new string(new char[256]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[64]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            ofn.lpstrTitle = dialogTitle;
            ofn.lpstrInitialDir = startingDirectory;

            if (GetOpenFileName(ref ofn))
                return ofn.lpstrFile;

            return string.Empty; // Return an empty string if the user cancels.
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int flagsEx;
    }
}
