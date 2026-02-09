using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class launcher : Page
    {
        // 1. Die Sicherung: Verhindert, dass Events feuern, während wir laden
        private bool _isInitializing = false;

        public launcher()
        {
            this.InitializeComponent();
            InitializeUI();
        }

        #region Methods

        private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }

        private void InitializeUI()
        {
            // 2. Sicherung AN
            _isInitializing = true;

            try
            {
                // --- 1. GFN PFAD ---
                try
                {
                    string gfnlauncherpath = AppSettings.Load<string>("gfnlauncherpath");
                    if (string.IsNullOrEmpty(gfnlauncherpath))
                    {
                        string roamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        gfnlauncherpath = System.IO.Path.Combine(roamingPath, @"Microsoft\Windows\Start Menu\Programs\NVIDIA GeForce NOW.lnk");
                        AppSettings.Save("gfnlauncherpath", gfnlauncherpath);
                    }
                    textbox_gfn_path.Text = gfnlauncherpath;
                }
                catch { }

                // --- 2. STEAM PFAD ---
                try
                {
                    string steamlauncherpath = AppSettings.Load<string>("steamlauncherpath");
                    textbox_steam_path.Text = steamlauncherpath ?? "";
                }
                catch { }

                // --- 3. PLAYNITE PFAD ---
                try
                {
                    string playnitelauncherpath = AppSettings.Load<string>("playnitelauncherpath");
                    textbox_playnite_path.Text = playnitelauncherpath ?? "";
                }
                catch { }

                // --- 4. CUSTOM PFAD ---
                try
                {
                    string customlauncherpath = AppSettings.Load<string>("customlauncherpath");
                    textbox_custom_path.Text = customlauncherpath ?? "";
                }
                catch { }

                // --- 5. SWITCHES SETZEN ---
                try
                {
                    string launcher = AppSettings.Load<string>("launcher");

                    switch (launcher)
                    {
                        case "steam":
                            use_steam_bp.IsOn = true;
                            use_playnite.IsOn = false;
                            use_custom.IsOn = false;
                            use_xbox.IsOn = false;
                            use_gfn.IsOn = false;
                            break;

                        case "playnite":
                            use_playnite.IsOn = true;
                            use_steam_bp.IsOn = false;
                            use_custom.IsOn = false;
                            use_xbox.IsOn = false;
                            use_gfn.IsOn = false;
                            break;

                        case "custom":
                            use_custom.IsOn = true;
                            use_playnite.IsOn = false;
                            use_steam_bp.IsOn = false;
                            use_xbox.IsOn = false;
                            use_gfn.IsOn = false;
                            break;

                        case "xbox":
                            use_xbox.IsOn = true;
                            use_custom.IsOn = false;
                            use_playnite.IsOn = false;
                            use_steam_bp.IsOn = false;
                            use_gfn.IsOn = false;
                            break;

                        case "gfn":
                            use_gfn.IsOn = true;
                            use_custom.IsOn = false;
                            use_playnite.IsOn = false;
                            use_steam_bp.IsOn = false;
                            use_xbox.IsOn = false;
                            break;

                        default:
                            launcher = "steam";
                            AppSettings.Save("launcher", launcher);
                            use_steam_bp.IsOn = true;
                            use_playnite.IsOn = false;
                            use_custom.IsOn = false;
                            use_xbox.IsOn = false;
                            use_gfn.IsOn = false;
                            break;
                    }

                    SteamPanel.Visibility = (launcher == "steam") ? Visibility.Visible : Visibility.Collapsed;
                    PlaynitePanel.Visibility = (launcher == "playnite") ? Visibility.Visible : Visibility.Collapsed;
                    CustomPanel.Visibility = (launcher == "custom") ? Visibility.Visible : Visibility.Collapsed;
                    XboxPanel.Visibility = (launcher == "xbox") ? Visibility.Visible : Visibility.Collapsed;
                    gfnPanel.Visibility = (launcher == "gfn") ? Visibility.Visible : Visibility.Collapsed;
                }
                catch
                {
                    use_steam_bp.IsOn = true;
                }
            }
            finally
            {
                // 3. Sicherung AUS
                _isInitializing = false;
            }
        }

        private string GetExePath()
        {
            string file = FilePicker.ShowDialog(
                "C:\\",
                new string[] { "exe" },
                "Executable Files",
                "Select an Executable File"
            );

            return string.IsNullOrEmpty(file) ? "none" : file;
        }

        // Helper Methode um Nachrichten anzuzeigen (Ersatz für MessageBox)
        private async Task ShowErrorDialog(string message, string title)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // Wichtig für WinUI 3
            };
            await dialog.ShowAsync();
        }

        private async Task CheckLauncherActivatedAsync()
        {
            if (_isInitializing) return;

            if (use_steam_bp.IsOn == false &
                use_playnite.IsOn == false &
                use_custom.IsOn == false &
                use_xbox.IsOn == false &
                use_gfn.IsOn == false)
            {
                await ShowErrorDialog("Please select at least one launcher. The default launcher will now be set.", "Information");

                AppSettings.Save("launcher", "steam");
                InitializeUI();
            }
        }

        #endregion Methods

        #region Events

        #region Steam Events
        private void textbox_steam_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            AppSettings.Save("steamlauncherpath", textbox_steam_path.Text);
        }

        private async void use_steam_bp_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (use_steam_bp.IsOn == true)
            {
                AppSettings.Save("launcher", "steam");
                InitializeUI();
            }
            else
            {
                await CheckLauncherActivatedAsync();
                SteamPanel.Visibility = Visibility.Collapsed;
            }
        }

        // WICHTIG: Hier async hinzugefügt und MessageBox ersetzt
        private async void pichsteampath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();
            if (exepath == "none") return;

            if (System.IO.Path.GetFileName(exepath).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                AppSettings.Save("steamlauncherpath", exepath);
                InitializeUI();
            }
            else
            {
                await ShowErrorDialog("Please select the correct file: steam.exe", "Invalid File");
            }
        }
        #endregion

        #region Playnite Events
        // WICHTIG: Hier async hinzugefügt und MessageBox ersetzt
        private async void pichplaynitepath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();
            if (exepath == "none") return;

            if (System.IO.Path.GetFileName(exepath).Equals("Playnite.FullscreenApp.exe", StringComparison.OrdinalIgnoreCase))
            {
                AppSettings.Save("playnitelauncherpath", exepath);
                InitializeUI();
            }
            else
            {
                await ShowErrorDialog("Please select the correct file: Playnite.FullscreenApp.exe", "Invalid File");
            }
        }

        private async void use_playnite_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (use_playnite.IsOn == true)
            {
                AppSettings.Save("launcher", "playnite");
                InitializeUI();
            }
            else
            {
                await CheckLauncherActivatedAsync();
                PlaynitePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void textbox_playnite_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            AppSettings.Save("playnitelauncherpath", textbox_playnite_path.Text);
        }
        #endregion

        #region Xbox Events
        private async void use_xbox_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (use_xbox.IsOn == true)
            {
                AppSettings.Save("launcher", "xbox");
                InitializeUI();
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
                AppSettings.Save("customlauncherpath", exepath);
                InitializeUI();
            }
        }

        private async void use_custom_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (use_custom.IsOn == true)
            {
                AppSettings.Save("launcher", "custom");
                InitializeUI();
            }
            else
            {
                await CheckLauncherActivatedAsync();
                CustomPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void textbox_custom_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            AppSettings.Save("customlauncherpath", textbox_custom_path.Text);
        }
        #endregion

        #region gfn Events
        private void pickgfnpath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = GetExePath();
            if (exepath == "none") return;
            textbox_gfn_path.Text = exepath;
            AppSettings.Save("gfnlauncherpath", exepath);
        }

        private void textbox_gfn_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            AppSettings.Save("gfnlauncherpath", textbox_gfn_path.Text);
        }

        private async void use_gfn_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            if (use_gfn.IsOn == true)
            {
                AppSettings.Save("launcher", "gfn");
                InitializeUI();
            }
            else
            {
                gfnPanel.Visibility = Visibility.Collapsed;
                await CheckLauncherActivatedAsync();
            }
        }
        #endregion

        #endregion Events
    }

    // Helper Klasse für den Datei-Dialog (unverändert)
    public static class FilePicker
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        public static string ShowDialog(string startingDirectory, string[] filters, string filterName, string dialogTitle)
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = filterName;
            foreach (string filter in filters)
            {
                ofn.lpstrFilter += $"\0*.{filter}";
            }
            ofn.lpstrFilter += "\0\0";

            ofn.lpstrFile = new string(new char[260]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[64]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            ofn.lpstrTitle = dialogTitle;
            ofn.lpstrInitialDir = startingDirectory;

            if (GetOpenFileName(ref ofn))
                return ofn.lpstrFile;

            return string.Empty;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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