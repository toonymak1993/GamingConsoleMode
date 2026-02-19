using Flurl.Http;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;
using Windows.UI;
using Button = Microsoft.UI.Xaml.Controls.Button;
using TextBox = Microsoft.UI.Xaml.Controls.TextBox;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class startup : Page
    {
        #region Class Members & Constructor

        private readonly DeckyInstallerLogic _deckyInstaller;
        private Storyboard ShowDetailStoryboard;
        private Storyboard HideDetailStoryboard;
        private List<Button> allDashboardButtons = new();
        private DispatcherTimer _timer;
        private bool updatetimer_once = false;

        public startup()
        {
            this.InitializeComponent();

            updateui();
            InitializeTimer();
            ShowDetailStoryboard = (Storyboard)this.Resources["ShowDetailPanelStoryboard"];
            HideDetailStoryboard = (Storyboard)this.Resources["HideDetailPanelStoryboard"];

            // Initialize the Decky Installer logic.
            _deckyInstaller = new DeckyInstallerLogic();

            // Store a reference to all buttons once the page is loaded.
            this.Loaded += (s, e) =>
            {
                allDashboardButtons = DashboardItems.Items.OfType<Button>().ToList();
            };
        }

        #endregion

        #region UI Update Timer

        private void InitializeTimer()
        {
            // Create a new DispatcherTimer that ticks every 3 seconds.
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            // Subscribe to the Tick event.
            _timer.Tick += Timer_Tick;

            // Start the timer.
            _timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            // Call the main update method on every tick to refresh the UI state.
            updateui();
        }

        #endregion

        #region Main UI Update Method

        private void updateui()
        {
            // The 'updatetimer_once' flag ensures some parts of the UI are only initialized once.
            if (updatetimer_once == false)
            {
                #region Pre-Audio (Initial Load)
                try
                {
                    bool preaudio = AppSettings.Load<bool>("usepreaudio");
                    if (preaudio)
                    {
                        // Dashboard Grid
                        preaudiobutton.Tag = "ENABLED";
                        audiodevicestate_color.Background = new SolidColorBrush(Colors.Green);
                        audiodevicestate_text.Text = "ENABLED";

                        use_preaudio.IsOn = true;
                        PopulateAudioDevices(false, true);

                        string preaudiostart = AppSettings.Load<string>("preaudiostart");
                        string preaudioend = AppSettings.Load<string>("preaudioend");

                        preaudio_end.PlaceholderText = preaudioend;
                        preaudio_start.PlaceholderText = preaudiostart;
                    }
                    else
                    {
                        use_preaudio.IsOn = false;

                        // Dashboard Grid
                        preaudiobutton.Tag = "DISABLED";
                        audiodevicestate_color.Background = new SolidColorBrush(Colors.Orange);
                        audiodevicestate_text.Text = "DISABLED";
                    }
                }
                catch
                {
                    Debug.WriteLine("Pre-audio GUI error on initial load.");
                }
                #endregion

                updatetimer_once = true;
            }

            // --- The following sections run on every timer tick ---

            #region Pre-Audio (Live Update)
            try
            {
                bool preaudio = AppSettings.Load<bool>("usepreaudio");
                if (preaudio)
                {
                    // Dashboard Grid
                    preaudiobutton.Tag = "ENABLED";
                    audiodevicestate_color.Background = new SolidColorBrush(Colors.Green);
                    audiodevicestate_text.Text = "ENABLED";
                    use_preaudio.IsOn = true;
                }
                else
                {
                    use_preaudio.IsOn = false;
                    // Dashboard Grid
                    preaudiobutton.Tag = "DISABLED";
                    audiodevicestate_color.Background = new SolidColorBrush(Colors.Orange);
                    audiodevicestate_text.Text = "DISABLED";
                }
            }
            catch
            {
                Debug.WriteLine("Pre-audio GUI error during live update.");
            }
            #endregion

            #region JoyXoff
            string joyxoffExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Joyxoff", "Joyxoff.exe");
            try
            {
                if (File.Exists(joyxoffExePath))
                {
                    text_install_state_joyxoff.Text = "INSTALLED";
                    border_install_state_joyxoff.Background = new SolidColorBrush(Colors.Green);
                    use_joyxoff.IsEnabled = true;

                    bool joyxofftogglestatus = AppSettings.Load<bool>("usejoyxoff");
                    if (joyxofftogglestatus)
                    {
                        use_joyxoff.IsOn = true;
                        // Dashboard Grid
                        joyxoffbutton.Tag = "ENABLED";
                        joyxoffstate_color.Background = new SolidColorBrush(Colors.Green);
                        joyxoffstate_text.Text = "ENABLED";
                    }
                    else
                    {
                        use_joyxoff.IsOn = false;
                        // Dashboard Grid
                        joyxoffbutton.Tag = "DISABLED";
                        joyxoffstate_color.Background = new SolidColorBrush(Colors.Orange);
                        joyxoffstate_text.Text = "DISABLED";
                    }
                }
                else
                {
                    text_install_state_joyxoff.Text = "NOT INSTALLED";
                    border_install_state_joyxoff.Background = new SolidColorBrush(Colors.Brown);
                    use_joyxoff.IsEnabled = false;
                    use_joyxoff.IsOn = false;
                    // Dashboard Grid (should be consistent with disabled state)
                    joyxoffbutton.Tag = "DISABLED";
                    joyxoffstate_color.Background = new SolidColorBrush(Colors.Orange);
                    joyxoffstate_text.Text = "DISABLED";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JoyXoff UI check failed: {ex.Message}");
            }
            #endregion

            #region DisplayFusion
            string registryPath = @"Software\Binary Fortress Software\DisplayFusion\MonitorConfig";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        // If the key exists, DF is considered installed.
                        text_install_state_displayfusion.Text = "INSTALLED";
                        border_install_state_displayfusion.Background = new SolidColorBrush(Colors.Green);
                        use_displayfusion.IsEnabled = true;
                        displayfusion_end.IsEnabled = true;

                        // Check the toggle state
                        bool displayfusiontogglestatus = AppSettings.Load<bool>("usedisplayfusion");
                        if (displayfusiontogglestatus)
                        {
                            use_displayfusion.IsOn = true;
                            // Dashboard Grid
                            displayfusionstate_color.Background = new SolidColorBrush(Colors.Green);
                            displayfusionstate_text.Text = "ENABLED";
                            displayfusionbutton.Tag = "ENABLED";
                        }
                        else
                        {
                            use_displayfusion.IsOn = false;
                            // Dashboard Grid
                            displayfusionstate_color.Background = new SolidColorBrush(Colors.Orange);
                            displayfusionstate_text.Text = "DISABLED";
                            displayfusionbutton.Tag = "DISABLED";
                        }

                        // Load saved profile names
                        string displayfusionstartprofile = AppSettings.Load<string>("usedisplayfusion_start");
                        string displayfusionendprofile = AppSettings.Load<string>("usedisplayfusion_end");
                        displayfusion_start.Text = displayfusionstartprofile;
                        displayfusion_end.Text = displayfusionendprofile;
                    }
                    else
                    {
                        // Key not found, DF is not installed or configured.
                        text_install_state_displayfusion.Text = "NOT INSTALLED OR NO DISPLAY CONFIG";
                        border_install_state_displayfusion.Background = new SolidColorBrush(Colors.Brown);
                        displayfusion_start.IsEnabled = false;
                        displayfusion_end.IsEnabled = false;
                        use_displayfusion.IsEnabled = false;
                        AppSettings.Save("usedisplayfusion", false);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading DisplayFusion registry: {ex.Message}");
            }
            #endregion

            #region GCM Wallpaper
            try
            {
                bool gcmwallpaper = AppSettings.Load<bool>("gcmwallpaper");
                if (gcmwallpaper)
                {
                    // Dashboard Grid
                    gcmwallpaperbutton.Tag = "ENABLED";
                    gcmwallpaperstate_color.Background = new SolidColorBrush(Colors.Green);
                    gcmwallpaperstate_text.Text = "ENABLED";

                    use_wallpaper.IsOn = true;
                    text_install_state_wallpaper.Text = "ACTIVATED";
                    border_install_state_wallpaper.Background = new SolidColorBrush(Colors.Green);
                    string wallpaperpath = AppSettings.Load<string>("gcmwallpaperpath");
                    wallpaper_path.Text = wallpaperpath;
                }
                else
                {
                    // Dashboard Grid
                    gcmwallpaperstate_color.Background = new SolidColorBrush(Colors.Orange);
                    gcmwallpaperstate_text.Text = "DISABLED";
                    gcmwallpaperbutton.Tag = "DISABLED";

                    use_wallpaper.IsOn = false;
                    text_install_state_wallpaper.Text = "DISABLED";
                    border_install_state_wallpaper.Background = new SolidColorBrush(Colors.Brown);
                    wallpaper_path.Text = "";
                }
            }
            catch
            {
                Debug.WriteLine("GCM Wallpaper GUI error.");
            }
            #endregion

            #region Lossless Scaling
            try
            {
                bool lossless = AppSettings.Load<bool>("lossless");
                if (lossless)
                {
                    // Dashboard Grid
                    losslessscalingbutton.Tag = "ENABLED";
                    losslessscaling_color.Background = new SolidColorBrush(Colors.Green);
                    losslessscaling_text.Text = "ENABLED";

                    use_lossless.IsOn = true;
                    text_install_state_lossless.Text = "ACTIVATED";
                    border_install_state_lossless.Background = new SolidColorBrush(Colors.Green);
                    string losslesspath = AppSettings.Load<string>("losslesspath");
                    lossless_path.Text = losslesspath;
                }
                else
                {
                    // Dashboard Grid
                    losslessscaling_color.Background = new SolidColorBrush(Colors.Orange);
                    losslessscaling_text.Text = "DISABLED";
                    losslessscalingbutton.Tag = "DISABLED";

                    use_lossless.IsOn = false;
                    text_install_state_lossless.Text = "DISABLED";
                    border_install_state_lossless.Background = new SolidColorBrush(Colors.Brown);
                    lossless_path.Text = "";
                }
            }
            catch
            {
                Debug.WriteLine("Lossless Scaling GUI error.");
            }
            #endregion

            #region Startup Video
            try
            {
                bool usestartupvideo = AppSettings.Load<bool>("usestartupvideo");
                if (usestartupvideo)
                {
                    text_install_state_Startup_Video.Text = "ACTIVATED";
                    border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Green);
                    use_startup_video.IsOn = true;

                    // Dashboard Grid
                    startupvideobutton.Tag = "ENABLED";
                    startupvideostate_color.Background = new SolidColorBrush(Colors.Green);
                    startupvideostate_text.Text = "ENABLED";
                }
                else
                {
                    text_install_state_Startup_Video.Text = "DISABLED";
                    border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Brown);
                    use_startup_video.IsOn = false;

                    // Dashboard Grid
                    startupvideobutton.Tag = "DISABLED";
                    startupvideostate_color.Background = new SolidColorBrush(Colors.Orange);
                    startupvideostate_text.Text = "DISABLED";
                }

                // Ensure path is set, defaulting if necessary
                string startupvideo_path = AppSettings.Load<string>("startupvideo_path");
                if (string.IsNullOrEmpty(startupvideo_path))
                {
                    startupvideo_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\GCM_Startup_Video.webm");
                    AppSettings.Save("startupvideo_path", startupvideo_path);
                }
                textbox_startupvideo_path.Text = startupvideo_path;

                // Update Steam injection checkbox
                if (AppSettings.Load<bool>("usesteamstartupvideo"))
                {
                    UseSteamStartupVideo.IsChecked = true;
                    Injectstartupvideo_button.IsEnabled = true;
                }
                else
                {
                    UseSteamStartupVideo.IsChecked = false;
                    Injectstartupvideo_button.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup Video GUI Error: {ex.Message}");
            }
            #endregion

            #region Decky Loader
            try
            {
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string homebrewPath = Path.Combine(userHome, "homebrew");
                string deckyloaderExePath = Path.Combine(homebrewPath, "services", "PluginLoader.exe");

                if (File.Exists(deckyloaderExePath))
                {
                    text_install_state_decky_loader.Text = "INSTALLED";
                    border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Green);
                    use_decky_loader.IsEnabled = true;

                    bool deckyloadertogglestatus = AppSettings.Load<bool>("usedeckyloader");
                    if (deckyloadertogglestatus)
                    {
                        use_decky_loader.IsOn = true;
                        // Dashboard Grid
                        deckyloaderbutton.Tag = "ENABLED";
                        deckyloaderstate_color.Background = new SolidColorBrush(Colors.Green);
                        deckyloaderstate_text.Text = "ENABLED";
                    }
                    else
                    {
                        use_decky_loader.IsOn = false;
                        // Dashboard Grid
                        deckyloaderbutton.Tag = "DISABLED";
                        deckyloaderstate_color.Background = new SolidColorBrush(Colors.Orange);
                        deckyloaderstate_text.Text = "DISABLED";
                    }
                }
                else
                {
                    text_install_state_decky_loader.Text = "NOT INSTALLED";
                    border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Brown);
                    use_decky_loader.IsEnabled = false;
                    use_decky_loader.IsOn = false;
                    AppSettings.Save("usedeckyloader", false);

                    // Dashboard Grid
                    deckyloaderbutton.Tag = "DISABLED";
                    deckyloaderstate_color.Background = new SolidColorBrush(Colors.Orange);
                    deckyloaderstate_text.Text = "DISABLED";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decky Loader UI check failed: {ex.Message}");
            }
            #endregion

            #region Preload List
            try
            {
                bool usepreloadlist = AppSettings.Load<bool>("usepreloadlist");
                if (usepreloadlist)
                {
                    text_install_state_preloadlist.Text = "ACTIVATED";
                    border_install_state_preloadlist.Background = new SolidColorBrush(Colors.Green);
                    use_preloadlist.IsOn = true;
                    string preloadListFilePath = AppSettings.Load<string>("prealoadlistpath");
                    preloadlist_path.Text = preloadListFilePath;

                    // Dashboard Grid
                    preloadlistbutton.Tag = "ENABLED";
                    preloadliststate_color.Background = new SolidColorBrush(Colors.Green);
                    preloadliststate_text.Text = "ENABLED";
                }
                else
                {
                    text_install_state_preloadlist.Text = "DISABLED";
                    border_install_state_preloadlist.Background = new SolidColorBrush(Colors.Brown);
                    use_preloadlist.IsOn = false;
                    AppSettings.Save("usepreloadlist", false);

                    // Dashboard Grid
                    preloadlistbutton.Tag = "DISABLED";
                    preloadliststate_color.Background = new SolidColorBrush(Colors.Orange);
                    preloadliststate_text.Text = "DISABLED";
                }
            }
            catch
            {
                Debug.WriteLine("Preload list GUI error.");
            }
            #endregion

            #region BoilR
            // Read API key from BoilR's config
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string boilrConfigPath = Path.Combine(appDataPath, "boilr", "config.toml");

                if (File.Exists(boilrConfigPath))
                {
                    string tomlText = File.ReadAllText(boilrConfigPath);
                    TomlTable toml = Toml.Parse(tomlText).ToModel();
                    var steamgridDb = toml["steamgrid_db"] as TomlTable;
                    if (steamgridDb != null)
                    {
                        string authKey = steamgridDb["auth_key"]?.ToString() ?? "";
                        boilr_path.Text = authKey;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error reading BoilR config for API key: " + ex.Message);
            }

            // Read launcher settings from BoilR's config
            LoadBoilrConfig();

            // Check BoilR installation state
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string boilrFolder = Path.Combine(appDataPath, "gcmsettings");
                string boilrExePath = Path.Combine(boilrFolder, "windows_BoilR.exe");

                if (File.Exists(boilrExePath))
                {
                    use_boilr.IsEnabled = true;
                    boilr_launcher.IsEnabled = true;
                    boilr_api_field.IsEnabled = true;
                    text_install_state_boilr.Text = "ACTIVATED";
                    border_install_state_boilr.Background = new SolidColorBrush(Colors.Green);
                    use_boilr.IsOn = true;

                    // Dashboard Grid
                    boilrbutton.Tag = "ENABLED";
                    boilrstate_color.Background = new SolidColorBrush(Colors.Green);
                    boilrstate_text.Text = "ENABLED";
                }
                else
                {
                    use_boilr.IsEnabled = false;
                    boilr_launcher.IsEnabled = false;
                    boilr_api_field.IsEnabled = false;
                    text_install_state_boilr.Text = "DISABLED";
                    border_install_state_boilr.Background = new SolidColorBrush(Colors.Brown);
                    use_boilr.IsOn = false;
                    AppSettings.Save("useboilr", false);

                    // Dashboard Grid
                    boilrbutton.Tag = "DISABLED";
                    boilrstate_color.Background = new SolidColorBrush(Colors.Orange);
                    boilrstate_text.Text = "DISABLED";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error checking BoilR path: " + ex.Message);
            }
            #endregion
        }

        #endregion

        #region Dashboard Filtering & Search

        private void FilterDashboardsearchButtons(string filterText)
        {
            // Ensure the master list of buttons is initialized.
            if (allDashboardButtons == null || !allDashboardButtons.Any())
                allDashboardButtons = DashboardItems.Items.OfType<Button>().ToList();

            DashboardItems.Items.Clear();

            // If the filter is empty, restore all buttons.
            if (string.IsNullOrWhiteSpace(filterText))
            {
                foreach (var button in allDashboardButtons)
                    DashboardItems.Items.Add(button);
                return;
            }

            string filter = filterText.ToLower();

            // Add buttons whose names contain the filter text.
            foreach (var btn in allDashboardButtons)
            {
                string buttonName = btn.Name?.ToLower() ?? "";
                if (buttonName.Contains(filter))
                    DashboardItems.Items.Add(btn);
            }
        }

        private void filterbuttonenabled_Click(object sender, RoutedEventArgs e)
        {
            DashboardItems.Items.Clear();
            foreach (var button in allDashboardButtons)
                DashboardItems.Items.Add(button);

            FilterDashboardButtons("ENABLED");
        }

        private void filterbuttondisabled_Click(object sender, RoutedEventArgs e)
        {
            DashboardItems.Items.Clear();
            foreach (var button in allDashboardButtons)
                DashboardItems.Items.Add(button);

            FilterDashboardButtons("DISABLED");
        }

        private void filterbuttonall_Click(object sender, RoutedEventArgs e)
        {
            DashboardItems.Items.Clear();
            foreach (var button in allDashboardButtons)
                DashboardItems.Items.Add(button);
        }

        private void FilterDashboardButtons(string filterTag)
        {
            // Take a copy of the current buttons to filter.
            var allButtons = DashboardItems.Items.OfType<Button>().ToList();
            DashboardItems.Items.Clear();

            // Add back only the buttons that match the filter tag.
            foreach (var btn in allButtons)
            {
                string tag = btn.Tag?.ToString() ?? "";
                if (tag.Equals(filterTag, StringComparison.OrdinalIgnoreCase))
                {
                    DashboardItems.Items.Add(btn);
                }
            }
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            FilterDashboardsearchButtons(SearchBox.Text);
        }

        #endregion

        #region Detail Panel Management

        private void Openpanel(string panelName)
        {
            // First, hide all detail panels.
            foreach (var child in DetailContentGrid.Children)
            {
                if (child is Grid grid)
                {
                    grid.Visibility = Visibility.Collapsed;
                }
            }

            // Then, make only the selected panel visible.
            var selectedPanel = DetailContentGrid.FindName(panelName) as Grid;
            if (selectedPanel != null)
            {
                selectedPanel.Visibility = Visibility.Visible;
            }

            // Show the main panel container.
            PanelView.Visibility = Visibility.Visible;

            // Run the "show" animation.
            ShowDetailStoryboard.Begin();

            // Hide the filter buttons and search box.
            filterbuttonall.Visibility = Visibility.Collapsed;
            filterbuttonenabled.Visibility = Visibility.Collapsed;
            filterbuttondisabled.Visibility = Visibility.Collapsed;
            SearchBox.Visibility = Visibility.Collapsed;
        }

        private void CloseDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            // Run the "hide" animation.
            HideDetailStoryboard.Completed += (s, args) =>
            {
                // After the animation is done, collapse the panel view.
                PanelView.Visibility = Visibility.Collapsed;
            };

            // Show the filter buttons and search box again.
            filterbuttonall.Visibility = Visibility.Visible;
            filterbuttonenabled.Visibility = Visibility.Visible;
            filterbuttondisabled.Visibility = Visibility.Visible;
            SearchBox.Visibility = Visibility.Visible;

            HideDetailStoryboard.Begin();
        }

        private void BreadcrumbBar1_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            // If the user clicks the root "Extensions" item, close the detail panel.
            if (args.Item is string clickedItem && clickedItem == "Extensions")
            {
                CloseDetailPanel_Click(null, null);
            }
        }

        #endregion

        #region Dashboard Button Click Handlers

      

        private void preloadlist_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("preloadlist");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Preload List" };
        }

        private void gcmwallpaperbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("gcmwallpaper");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Task Manager Wallpaper" };
        }

        private void joyxoffbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("JoyxoffPanel");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "JoyXOff" };
        }

        private void deckyloaderbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("deckyloader");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Decky Loader" };
        }

        private void preaudiobutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("preaudio");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Audio Device" };
        }

        private void displayfusionbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("displayfusion");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "DisplayFusion" };
        }

        private void startupvideobutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("startupvideo");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Startup Video" };
        }

        private void boilrbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("boilr");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "BoilR Game Sync" };
        }

        private void losslessscalingbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("lossless");
            BreadcrumbBar1.ItemsSource = new string[] { "Extensions", "Lossless Scaling" };
        }

        #endregion

        #region Feature Handlers: Preload List

        private void btn_create_or_open_preloadlist_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string gcmFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
                Directory.CreateDirectory(gcmFolderPath); // Ensure the folder exists.
                string preloadListFilePath = Path.Combine(gcmFolderPath, "preloadlist.txt");

                if (!File.Exists(preloadListFilePath))
                {
                    File.WriteAllText(preloadListFilePath, "# Add one executable name per line, for example: discord.exe");
                }

                // Save the path and update the UI.
                AppSettings.Save("prealoadlistpath", preloadListFilePath);
                preloadlist_path.Text = preloadListFilePath;

                // Open the file with the default editor.
                Process.Start(new ProcessStartInfo { FileName = preloadListFilePath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Could not open the file.\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
        }

        private void preloadlist_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            // This event handler is currently not used.
        }

        private void use_preloadlist_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (use_preloadlist.IsOn)
                {
                    AppSettings.Save("usepreloadlist", true);
                    text_install_state_preloadlist.Text = "ACTIVATED";
                    border_install_state_preloadlist.Background = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    AppSettings.Save("usepreloadlist", false);
                    text_install_state_preloadlist.Text = "DISABLED";
                    border_install_state_preloadlist.Background = new SolidColorBrush(Colors.Brown);
                }
            }
            catch
            {
                Debug.WriteLine("Problem with preload list integration.");
            }
        }

        #endregion

        #region Feature Handlers: Pre-Audio

        private void preaudio_start_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
                {
                    string selectedDeviceName = comboBox.SelectedItem.ToString();
                    string cleanedDeviceName = selectedDeviceName.Split('(')[0].Trim();

                    AppSettings.Save("preaudiostart", cleanedDeviceName);
                    Debug.WriteLine($"Saved Start Device Name: {cleanedDeviceName}");
                    NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedDeviceName}\"");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving selected device: {ex.Message}");
            }
        }

        private void preaudio_end_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
                {
                    string selectedDeviceName = comboBox.SelectedItem.ToString();
                    string cleanedDeviceName = selectedDeviceName.Split('(')[0].Trim();

                    AppSettings.Save("preaudioend", cleanedDeviceName);
                    Debug.WriteLine($"Saved End Device Name: {cleanedDeviceName}");
                    NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedDeviceName}\"");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving selected device: {ex.Message}");
            }
        }

        private void use_preaudio_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                PopulateAudioDevices(false, true);

                if (use_preaudio.IsOn)
                {
                    AppSettings.Save("usepreaudio", true);
                    preaudio_end.IsEnabled = true;
                    preaudio_start.IsEnabled = true;
                    text_install_state_preaudio.Text = "ACTIVATED";
                    border_install_state_preaudio.Background = new SolidColorBrush(Colors.Green);
                }
                else
                {
                    AppSettings.Save("usepreaudio", false);
                    preaudio_end.IsEnabled = false;
                    preaudio_start.IsEnabled = false;
                    text_install_state_preaudio.Text = "DISABLED";
                    border_install_state_preaudio.Background = new SolidColorBrush(Colors.Brown);
                }
            }
            catch
            {
                Debug.WriteLine("Problem with audio device integration.");
            }
        }

        private void PopulateAudioDevices(bool discord, bool preaudio)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();

                preaudio_end.Items.Clear();
                preaudio_start.Items.Clear();

                // Get all active playback devices.
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    preaudio_end.Items.Add(device.FriendlyName);
                    preaudio_start.Items.Add(device.FriendlyName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while populating audio devices: {ex.Message}");
            }
        }

        #endregion

        #region Feature Handlers: Decky Loader

        private async void button_install_decky_loader_Click(object sender, RoutedEventArgs e)
        {
            // Disable buttons to prevent multiple clicks during installation.
            button_install_decky_loader.IsEnabled = false;
            button_install_decky_loader.Content = "Installing...";
            button_uninstall_decky_loader.IsEnabled = false;

            try
            {
                // Call the installation method from the logic class.
                await _deckyInstaller.ExecuteInstallationAsync();
                updateui(); // Refresh the UI after installation.

                ContentDialog successDialog = new ContentDialog
                {
                    Title = "Installation Successful",
                    Content = "Decky Loader has been successfully installed!",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Installation Error",
                    Content = $"An error occurred during installation:\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                // Re-enable buttons and restore text, regardless of outcome.
                button_install_decky_loader.IsEnabled = true;
                button_uninstall_decky_loader.IsEnabled = true;
                button_install_decky_loader.Content = "Install Decky Loader";
            }
        }

        private async void button_uninstall_decky_loader_Click(object sender, RoutedEventArgs e)
        {
            button_uninstall_decky_loader.IsEnabled = false;
            button_install_decky_loader.IsEnabled = false;
            button_uninstall_decky_loader.Content = "Uninstalling...";

            try
            {
                await _deckyInstaller.ExecuteUninstallationAsync();
                AppSettings.Save("usedeckyloader", false);
                updateui();

                ContentDialog successDialog = new ContentDialog
                {
                    Title = "Uninstallation Successful",
                    Content = "Decky Loader has been successfully removed.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                AppSettings.Save("usedeckyloader", false);
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Uninstallation Error",
                    Content = $"An error occurred during uninstallation:\n\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                button_uninstall_decky_loader.IsEnabled = true;
                button_install_decky_loader.IsEnabled = true;
                button_uninstall_decky_loader.Content = "Uninstall Decky Loader";
            }
        }

        private void use_decky_loader_Toggled_1(object sender, RoutedEventArgs e)
        {
            if (use_decky_loader.IsOn)
            {
                AppSettings.Save("usedeckyloader", true);
                text_install_state_decky_loader.Text = "INSTALLED"; // Assuming toggle is only enabled if installed
                border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                AppSettings.Save("usedeckyloader", false);
                // The text remains "INSTALLED" as the files are still there, just not in use.
            }
        }

        #endregion

        #region Feature Handlers: Startup Video

        private void textbox_startupvideo_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                AppSettings.Save("startupvideo_path", textbox_startupvideo_path.Text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving startup video path: {ex.Message}");
            }
        }

        private void use_startup_video_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_startup_video.IsOn)
            {
                AppSettings.Save("usestartupvideo", true);
                text_install_state_Startup_Video.Text = "ACTIVATED";
                border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                AppSettings.Save("usestartupvideo", false);
                text_install_state_Startup_Video.Text = "DISABLED";
                border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Brown);
            }
        }

        private void pichstartupvideopath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                pichstartupvideopath.IsEnabled = false;
                // CORRECTED: The call now directly uses FilePicker from the namespace.
                string? file = FilePicker.ShowDialog(
                    "C:\\",  // Starting folder
                    new string[] { "*" },  // Accepted extensions
                    "*",  // Full filter with correct syntax
                    "Select a video file"  // Dialog box title
                );

                if (!string.IsNullOrEmpty(file))
                {
                    AppSettings.Save("startupvideo_path", file);
                    textbox_startupvideo_path.Text = file;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error when selecting the file: {ex.Message}");
            }
            finally
            {
                pichstartupvideopath.IsEnabled = true;
            }
        }

        private void UseSteamStartupVideoCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("usesteamstartupvideo", true);
            Injectstartupvideo_button.IsEnabled = true;
            textbox_select_startupvideo_path.Visibility = Visibility.Collapsed;
        }

        private void UseSteamStartupVideoCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("usesteamstartupvideo", false);
            Injectstartupvideo_button.IsEnabled = false;
            textbox_select_startupvideo_path.Visibility = Visibility.Visible;
        }

        private void Injectstartupvideo_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Injectstartupvideo_Text.Text = "";
                Injectstartupvideo_Text.Visibility = Visibility.Visible;
                Injectstartupvideo_ProgressBar.Visibility = Visibility.Visible;
                Injectstartupvideo_ProgressBar.Value = 0;
                Injectstartupvideo_button.IsEnabled = false;

                string videoPath = AppSettings.Load<string>("startupvideo_path");
                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    throw new Exception("No valid video file selected.");
                }

                if (Path.GetExtension(videoPath).ToLower() != ".webm")
                {
                    throw new Exception("The selected file is not in WebM format.");
                }

                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (steamPath.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase))
                {
                    steamPath = Path.GetDirectoryName(steamPath);
                }

                string outputPath = Path.Combine(steamPath, "steamui", "movies", "GCM_vid.webm");
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

                Injectstartupvideo_ProgressBar.Value = 50;
                File.Copy(videoPath, outputPath, true);

                Injectstartupvideo_ProgressBar.Value = 100;
                Injectstartupvideo_Text.Text = "Video copied successfully.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR injecting video: {ex.Message}");
                Injectstartupvideo_Text.Text = "Error: " + ex.Message;
            }
            finally
            {
                Injectstartupvideo_button.IsEnabled = true;
            }
        }

        private void Resetstartupvideopath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string startupvideo_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\GCM_Startup_Video.webm");
                AppSettings.Save("startupvideo_path", startupvideo_path);
                textbox_startupvideo_path.Text = startupvideo_path;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting startup video path: {ex.Message}");
            }
        }

        #endregion

        #region Feature Handlers: DisplayFusion

        private void use_displayfusion_Toggled(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("usedisplayfusion", use_displayfusion.IsOn);
        }

        private void button_uninstall_displayfusion_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening Windows settings: " + ex.Message);
            }
        }

        private void button_install_displayfusion_Click_2(object sender, RoutedEventArgs e)
        {
            string url = "https://www.displayfusion.com/download/";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching webpage: {ex.Message}");
            }
        }

        private void displayfusion_start_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("usedisplayfusion_start", displayfusion_start.Text);
        }

        private void displayfusion_end_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("usedisplayfusion_end", displayfusion_end.Text);
        }

        #endregion

        #region Feature Handlers: GCM Wallpaper

        #region Feature Handlers: Online Wallpaper Search

        // Data Model
        public class WallpaperItem
        {
            public string ThumbnailUrl { get; set; }
            public string FullUrl { get; set; }
            public string Id { get; set; }
            public string Resolution { get; set; }
        }

        // State Tracking for Pagination
        private int _currentPage = 1;
        private string _currentQuery = "gaming";
        private string _currentResolution = "";

        // Trigger search on Enter key
        private void WallpaperSearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                BtnSearchWallpaper_Click(sender, new RoutedEventArgs());
            }
        }

        // --- SEARCH BUTTON (Reset to Page 1) ---
        private void BtnSearchWallpaper_Click(object sender, RoutedEventArgs e)
        {
            // 1. Get Values
            string query = WallpaperSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query)) query = "gaming";
            _currentQuery = query;

            _currentResolution = "";
            if (WallpaperResolutionCombo.SelectedItem is ComboBoxItem resItem && resItem.Tag != null)
            {
                _currentResolution = resItem.Tag.ToString();
            }

            // 2. Reset Page
            _currentPage = 1;

            // 3. Execute
            PerformSearch(_currentPage);
        }

        // --- NEXT / PREV BUTTONS ---
        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            PerformSearch(_currentPage);
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                PerformSearch(_currentPage);
            }
        }

        // --- CORE SEARCH LOGIC ---
        private async void PerformSearch(int page)
        {
            WallpaperLoadingBar.Visibility = Visibility.Visible;
            WallpaperResultsGrid.ItemsSource = null;

            // Update UI Controls
            TxtPageNumber.Text = $"Page {page}";
            BtnPrevPage.IsEnabled = page > 1;
            BtnNextPage.IsEnabled = false; // Disable until load complete

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("GCM-App/1.0");

                    // Build URL
                    string apiUrl = $"https://wallhaven.cc/api/v1/search?q={Uri.EscapeDataString(_currentQuery)}&purity=100&sorting=relevance&page={page}";

                    if (!string.IsNullOrEmpty(_currentResolution))
                    {
                        apiUrl += $"&resolutions={_currentResolution}";
                    }

                    // Call API
                    string jsonResponse = await client.GetStringAsync(apiUrl);

                    // Parse
                    var jsonDoc = JsonDocument.Parse(jsonResponse);
                    var root = jsonDoc.RootElement;
                    var dataArray = root.GetProperty("data");

                    List<WallpaperItem> wallpapers = new List<WallpaperItem>();

                    foreach (var item in dataArray.EnumerateArray())
                    {
                        try
                        {
                            string fullPath = item.GetProperty("path").GetString();
                            string thumbPath = item.GetProperty("thumbs").GetProperty("small").GetString();
                            string id = item.GetProperty("id").GetString();
                            string res = item.GetProperty("resolution").GetString();

                            wallpapers.Add(new WallpaperItem
                            {
                                ThumbnailUrl = thumbPath,
                                FullUrl = fullPath,
                                Id = id,
                                Resolution = res
                            });
                        }
                        catch { /* Skip bad items */ }
                    }

                    if (wallpapers.Count == 0)
                    {
                        Debug.WriteLine("No wallpapers found.");
                    }

                    WallpaperResultsGrid.ItemsSource = wallpapers;

                    // Enable Next button if we found results (simple logic)
                    BtnNextPage.IsEnabled = wallpapers.Count > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Wallpaper Search Error: {ex.Message}");
                ContentDialog errorDialog = new ContentDialog
                {
                    Title = "Search Error",
                    Content = "Could not fetch wallpapers. Please try again.",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                _ = errorDialog.ShowAsync();
            }
            finally
            {
                WallpaperLoadingBar.Visibility = Visibility.Collapsed;
            }
        }

        // --- DOWNLOAD & APPLY ---
        private async void WallpaperResultsGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is WallpaperItem selectedWallpaper)
            {
                WallpaperLoadingBar.Visibility = Visibility.Visible;

                try
                {
                    string gcmFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "wallpapers");
                    Directory.CreateDirectory(gcmFolder);

                    string extension = Path.GetExtension(selectedWallpaper.FullUrl) ?? ".jpg";
                    string localPath = Path.Combine(gcmFolder, $"gcm_wp_{selectedWallpaper.Id}{extension}");

                    using (HttpClient client = new HttpClient())
                    {
                        byte[] imageBytes = await client.GetByteArrayAsync(selectedWallpaper.FullUrl);
                        await File.WriteAllBytesAsync(localPath, imageBytes);
                    }

                    AppSettings.Save("gcmwallpaperpath", localPath);
                    wallpaper_path.Text = localPath;

                    if (!use_wallpaper.IsOn)
                    {
                        use_wallpaper.IsOn = true;
                    }

                    ContentDialog successDialog = new ContentDialog
                    {
                        Title = "Wallpaper Applied",
                        Content = $"Downloaded page {_currentPage} image.\nResolution: {selectedWallpaper.Resolution}\nSet successfully!",
                        CloseButtonText = "Awesome!",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    ContentDialog errorDialog = new ContentDialog
                    {
                        Title = "Download Failed",
                        Content = $"Could not download image: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = this.Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
                finally
                {
                    WallpaperLoadingBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        private void wallpaper_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("gcmwallpaperpath", wallpaper_path.Text);
        }

        private void use_wallpaper_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_wallpaper.IsOn)
            {
                AppSettings.Save("gcmwallpaper", true);
                text_install_state_wallpaper.Text = "ACTIVATED";
                border_install_state_wallpaper.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                AppSettings.Save("gcmwallpaper", false);
                text_install_state_wallpaper.Text = "DISABLED";
                border_install_state_wallpaper.Background = new SolidColorBrush(Colors.Brown);
                wallpaper_path.Text = "";
            }
        }

        #endregion

        #region Feature Handlers: JoyXoff

        private void button_uninstall_joyxoff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error opening Windows settings: " + ex.Message);
            }
        }

        private void button_install_joyxoff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDownloadUrl = "https://joyxoff.com/download.php?culture=en";
                string pageContent;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    pageContent = wc.DownloadString(baseDownloadUrl);
                }

                string pattern = @"download\.php\?culture=en&version=([\d\.]+)";
                var match = Regex.Match(pageContent, pattern);
                string version = match.Success ? match.Groups[1].Value : "3.63.10.7"; // Fallback version
                string downloadUrl = $"https://joyxoff.com/download.php?culture=en&version={version}";
                string downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "joyxoffInstaller");
                Directory.CreateDirectory(downloadFolder);
                string fileName = "joyxoff.rar";

                downloadUrl.DownloadFileAsync(downloadFolder, fileName).GetAwaiter().GetResult();
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{downloadFolder}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("JoyXoff download error: " + ex.Message);
            }
        }

        private void use_joyxoff_Toggled(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("usejoyxoff", use_joyxoff.IsOn);
        }
        #endregion

        #region Feature Handlers: BoilR

        private void use_boilr_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_boilr.IsOn)
            {
                // Logic to enable BoilR, if any, beyond just saving the setting.
                // Currently handled by the download/install process.
            }
            else
            {
                // Logic to uninstall/cleanup BoilR.
                try
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string gcmSettingsFolder = Path.Combine(appDataPath, "gcmsettings");
                    string boilrExePath = Path.Combine(gcmSettingsFolder, "windows_BoilR.exe");
                    if (File.Exists(boilrExePath)) File.Delete(boilrExePath);

                    string boilrFolder = Path.Combine(appDataPath, "boilr");
                    if (Directory.Exists(boilrFolder)) Directory.Delete(boilrFolder, true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error deleting BoilR files: " + ex.Message);
                }
            }
        }

        private void btn_create_or_open_boilr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening SteamGridDB API page: {ex.Message}");
            }
        }

        private void boilr_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            string newAuthKey = (sender as TextBox).Text;
            try
            {
                string boilrConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "boilr", "config.toml");
                if (!File.Exists(boilrConfigPath)) return;

                string tomlText = File.ReadAllText(boilrConfigPath);
                TomlTable toml = Toml.Parse(tomlText).ToModel();
                if (toml["steamgrid_db"] is TomlTable steamgridDb)
                {
                    steamgridDb["auth_key"] = newAuthKey;
                    File.WriteAllText(boilrConfigPath, Toml.FromModel(toml));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error updating BoilR config: " + ex.Message);
            }
        }

        private void UpdateBoilrConfig(string sectionName, bool enabled)
        {
            try
            {
                string boilrConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "boilr", "config.toml");
                if (!File.Exists(boilrConfigPath)) return;

                string tomlText = File.ReadAllText(boilrConfigPath);
                TomlTable toml = Toml.Parse(tomlText).ToModel();
                if (toml.ContainsKey(sectionName) && toml[sectionName] is TomlTable section)
                {
                    section["enabled"] = enabled;
                    File.WriteAllText(boilrConfigPath, Toml.FromModel(toml));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error updating BoilR config: " + ex.Message);
            }
        }

        private void LoadBoilrConfig()
        {
            try
            {
                string boilrConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "boilr", "config.toml");
                if (!File.Exists(boilrConfigPath)) return;

                string tomlText = File.ReadAllText(boilrConfigPath);
                TomlTable toml = Toml.Parse(tomlText).ToModel();

                SetSwitchState("amazon", Switch_Amazon, toml);
                SetSwitchState("epic_games", Switch_Epic, toml);
                SetSwitchState("gog", Switch_GOG, toml);
                SetSwitchState("itch", Switch_Itch, toml);
                SetSwitchState("origin", Switch_Origin, toml);
                SetSwitchState("uplay", Switch_Uplay, toml);
                SetSwitchState("playnite", Switch_Playnite, toml);
                SetSwitchState("gamepass", Switch_GamePass, toml);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error reading BoilR config: " + ex.Message);
            }
        }

        public string CopyBoilrConfigFile()
        {
            try
            {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "functions", "config.toml");
                if (!File.Exists(sourcePath))
                {
                    return $"Source config.toml not found: {sourcePath}";
                }

                string boilrFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "boilr");
                Directory.CreateDirectory(boilrFolder);
                string targetPath = Path.Combine(boilrFolder, "config.toml");

                File.Copy(sourcePath, targetPath, true);
                AppSettings.Save("useboilr", true);
                return $"BoilR config.toml copied successfully to {targetPath}";
            }
            catch (Exception ex)
            {
                return "Error copying BoilR config.toml: " + ex.Message;
            }
        }

        private void SetSwitchState(string sectionName, ToggleSwitch toggleSwitch, TomlTable toml)
        {
            if (toml.ContainsKey(sectionName) && toml[sectionName] is TomlTable section && section.ContainsKey("enabled"))
            {
                toggleSwitch.IsOn = Convert.ToBoolean(section["enabled"]);
            }
        }

        // Event Handlers for each launcher toggle in BoilR
        private void Switch_Amazon_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("amazon", (sender as ToggleSwitch).IsOn);
        private void Switch_Epic_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("epic_games", (sender as ToggleSwitch).IsOn);
        private void Switch_GOG_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("gog", (sender as ToggleSwitch).IsOn);
        private void Switch_Itch_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("itch", (sender as ToggleSwitch).IsOn);
        private void Switch_Origin_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("origin", (sender as ToggleSwitch).IsOn);
        private void Switch_Uplay_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("uplay", (sender as ToggleSwitch).IsOn);
        private void Switch_Playnite_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("playnite", (sender as ToggleSwitch).IsOn);
        private void Switch_GamePass_Toggled(object sender, RoutedEventArgs e) => UpdateBoilrConfig("gamepass", (sender as ToggleSwitch).IsOn);

        public string DownloadAndRunBoilr()
        {
            try
            {
                string apiUrl = "https://api.github.com/repos/PhilipK/BoilR/releases/latest";
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GCM-Boilr-Downloader");

                string json = httpClient.GetStringAsync(apiUrl).Result;
                using var doc = JsonDocument.Parse(json);
                var assets = doc.RootElement.GetProperty("assets");
                var exeAsset = assets.EnumerateArray().FirstOrDefault(asset => asset.GetProperty("name").GetString().EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

                if (exeAsset.ValueKind == JsonValueKind.Undefined) return "No EXE found in latest release.";

                string downloadUrl = exeAsset.GetProperty("browser_download_url").GetString();
                string fileName = exeAsset.GetProperty("name").GetString();
                string targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
                Directory.CreateDirectory(targetFolder);
                string targetPath = Path.Combine(targetFolder, fileName);

                if (!File.Exists(targetPath))
                {
                    using (var webClient = new WebClient())
                    {
                        webClient.Headers.Add("User-Agent", "GCM-Boilr-Downloader");
                        webClient.DownloadFile(downloadUrl, targetPath);
                    }
                }

                return CopyBoilrConfigFile();
            }
            catch (Exception ex)
            {
                return "Error downloading or copying BoilR config: " + ex.Message;
            }
        }

        private void btnDownloadBoilr_Click(object sender, RoutedEventArgs e)
        {
            txtDownloadStatus.Text = DownloadAndRunBoilr();
        }
        #endregion

        #region Feature Handlers: Lossless Scaling
        private void lossless_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("losslesspath", lossless_path.Text);
        }

        private void use_lossless_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_lossless.IsOn)
            {
                AppSettings.Save("lossless", true);
                text_install_state_lossless.Text = "ACTIVATED";
                border_install_state_lossless.Background = new SolidColorBrush(Colors.Green);

                if (string.IsNullOrWhiteSpace(lossless_path.Text))
                {
                    lossless_path.Text = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Lossless Scaling\\LosslessScaling.exe";
                }
            }
            else
            {
                AppSettings.Save("lossless", false);
                text_install_state_lossless.Text = "DISABLED";
                border_install_state_lossless.Background = new SolidColorBrush(Colors.Brown);
                lossless_path.Text = "";
            }
        }

        #endregion

        #region Visual Tree & Misc Helpers

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    var child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                        yield return t;

                    foreach (var childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }

        private string GetFirstTextBlockText(DependencyObject parent)
        {
            return FindVisualChildren<TextBlock>(parent)
                   .FirstOrDefault(child => !string.IsNullOrWhiteSpace(child.Text))?.Text;
        }

        #endregion
    }

    #region NirCmd Helper Class
    namespace NirCmdUtil
    {
        public static class NirCmdHelper
        {
            /// <summary>
            /// Executes a command using nircmd.exe.
            /// </summary>
            /// <param name="command">The command to pass to nircmd (e.g., "changesysvolume 5000")</param>
            public static void ExecuteCommand(string command)
            {
                string nircmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nircmd.exe");

                if (!File.Exists(nircmdPath))
                {
                    throw new FileNotFoundException("nircmd.exe was not found in the application directory.");
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = nircmdPath,
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        throw new Exception("Error executing nircmd command: " + error);
                    }
                }
            }
        }
    }
    #endregion
}

