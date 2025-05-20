using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;
using Flurl.Http;
using System.Text.Json;
using System.Net.Http;
using static GAMINGCONSOLEMODE.launcher;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Windows.UI;
using System.Drawing;
using System.Threading;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class startup : Page
    {
        #region generate Dashboard function
       
   


        private Storyboard ShowDetailStoryboard;
        private Storyboard HideDetailStoryboard;

        private List<Button> allDashboardButtons = new(); 

        #endregion generate Dashboard function
        public startup()
        {
            this.InitializeComponent();
            updateui();
            InitializeTimer();
            ShowDetailStoryboard = (Storyboard)this.Resources["ShowDetailPanelStoryboard"];
            HideDetailStoryboard = (Storyboard)this.Resources["HideDetailPanelStoryboard"];

            // save Buttons, at start
            this.Loaded += (s, e) =>
            {
                allDashboardButtons = DashboardItems.Items.OfType<Button>().ToList();
            };


        }


        #region filter

        private void FilterDashboardsearchButtons(string filterTag)
        {
            if (allDashboardButtons == null)
                allDashboardButtons = DashboardItems.Items.OfType<Button>().ToList();

            DashboardItems.Items.Clear();

            if (string.IsNullOrWhiteSpace(filterTag))
            {
                foreach (var button in allDashboardButtons)
                    DashboardItems.Items.Add(button);
                return;
            }

            string filter = filterTag.ToLower();

            foreach (var btn in allDashboardButtons)
            {
                string tag = btn.Name?.ToLower() ?? "";

                if (tag.Contains(filter))
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
            // Alle Buttons im ItemsControl speichern
            var allButtons = DashboardItems.Items.OfType<Button>().ToList();

            // ItemsControl leeren
            DashboardItems.Items.Clear();

            foreach (var btn in allButtons)
            {
                string tag = btn.Tag?.ToString() ?? "";

                if (tag.Equals(filterTag, StringComparison.OrdinalIgnoreCase))
                {
                    DashboardItems.Items.Add(btn); // nur erlaubte Buttons
                }
            }
        }


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


        #endregion filter
        #region code functions
        //need variable
        private DispatcherTimer _timer;
        bool updatetimer_once = false;

        #region update ui

        private void InitializeTimer()
        {
            // Create a new DispatcherTimer with a 10-second interval
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            // Subscribe to the Tick event
            _timer.Tick += Timer_Tick;

            // Start the timer
            _timer.Start();
        }
        private void Timer_Tick(object sender, object e)
        {
            // Call your update method on every tick
            updateui();
        }

        

       






        private void updateui()
        {

            if (updatetimer_once == false)
            {

                #region preaudio
                try
                {
                    bool preaudio = AppSettings.Load<bool>("usepreaudio");
                    if (preaudio == true)
                    {

                        //Dashboardgrid
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

                        //Dashboardgrid
                        preaudiobutton.Tag = "DISABLED";
                        audiodevicestate_color.Background = new SolidColorBrush(Colors.Orange);
                        audiodevicestate_text.Text = "DISABLED";
                    }
                }
                catch
                {
                    Console.WriteLine("preaudio gui error");
                }
                #endregion

                updatetimer_once = true;
            }

            #region preaudio
            try
            {
                bool preaudio = AppSettings.Load<bool>("usepreaudio");
                if (preaudio == true)
                {

                    //Dashboardgrid
                    preaudiobutton.Tag = "ENABLED";
                    audiodevicestate_color.Background = new SolidColorBrush(Colors.Green);
                    audiodevicestate_text.Text = "ENABLED";

                    use_preaudio.IsOn = true;

                }
                else
                {
                    use_preaudio.IsOn = false;

                    //Dashboardgrid
                    preaudiobutton.Tag = "DISABLED";
                    audiodevicestate_color.Background = new SolidColorBrush(Colors.Orange);
                    audiodevicestate_text.Text = "DISABLED";
                }
            }
            catch
            {
                Console.WriteLine("preaudio gui error");
            }
            #endregion
            #region cssloader
            try
            {
                string cssloaderExePath = @"C:\Program Files\CSSLoader Desktop\CSSLoader Desktop.exe";
                if (File.Exists(cssloaderExePath))
                {
                    text_install_state_cssloader.Text = "INSTALLED";
                    border_install_state_cssloader.Background = new SolidColorBrush(Colors.Green);
                    use_cssloader.IsEnabled = true;

                    //toggle ui
                    bool cssloadertogglestatus = AppSettings.Load<bool>("usecssloader");
                    if (cssloadertogglestatus == true)
                    {
                        use_cssloader.IsOn = true;
                        //Dashboardgrid
                        cssloaderbutton.Tag = "ENABLED";
                        cssloaderstate_color.Background = new SolidColorBrush(Colors.Green);
                        cssloaderstate_text.Text = "ENABLED";

                    }
                    else
                    {
                        use_cssloader.IsOn = false;
                        //Dashboardgrid
                        cssloaderstate_color.Background = new SolidColorBrush(Colors.Orange);
                        cssloaderstate_text.Text = "DISABLED";
                        cssloaderbutton.Tag = "DISABLED";
                    }
                }
                else
                {
                    text_install_state_cssloader.Text = "NOT INSTALLED";
                    border_install_state_cssloader.Background = new SolidColorBrush(Colors.Brown);
                    use_cssloader.IsEnabled = false;
                    use_cssloader.IsOn = false;

                }
            }
            catch
            {

            }
            #endregion cssloader
            #region joyxoff
            string joyxoffExePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Joyxoff", "Joyxoff.exe");

            try
            {
                if (File.Exists(joyxoffExePath))
                {
                    text_install_state_joyxoff.Text = "INSTALLED";
                    border_install_state_joyxoff.Background = new SolidColorBrush(Colors.Green);
                    use_joyxoff.IsEnabled = true;
                    //toggle ui
                    bool joyxofftogglestatus = AppSettings.Load<bool>("usejoyxoff");
                    if (joyxofftogglestatus == true)
                    {
                        use_joyxoff.IsOn = true;
                        //Dashboardgrid
                        joyxoffbutton.Tag = "ENABLED";
                        joyxoffstate_color.Background = new SolidColorBrush(Colors.Green);
                        joyxoffstate_text.Text = "ENABLED";

                    }
                    else
                    {
                        use_joyxoff.IsOn = false;
                        //Dashboardgrid
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

                    //Dashboardgrid
                    gcmwallpaperbutton.Tag = "DISABLED";
                    gcmwallpaperstate_color.Background = new SolidColorBrush(Colors.Orange);
                    gcmwallpaperstate_text.Text = "DISABLED";

                }
            }
            catch
            {

            }



            #endregion joyxoff
            #region displayfusion
            // Registry path for DisplayFusion profiles
            string registryPath = @"Software\Binary Fortress Software\DisplayFusion\MonitorConfig";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key != null)
                    {
                        // Iterate over all profiles in the registry
                        foreach (string profileName in key.GetSubKeyNames())
                        {
                            using (RegistryKey profileKey = key.OpenSubKey(profileName))
                            {
                                if (profileKey != null)
                                {
                                    // Read data from the registry
                                    string name = profileKey.GetValue("Name")?.ToString();
                                    string audioPlaybackMM = profileKey.GetValue("AudioPlaybackMM")?.ToString();
                                    string monitorJson = profileKey.GetValue("Monitor0_Json")?.ToString();


                                    //Displayfusion installed
                                    text_install_state_displayfusion.Text = "INSTALLED";
                                    border_install_state_displayfusion.Background = new SolidColorBrush(Colors.Green);
                                    use_displayfusion.IsEnabled = true;
                                    displayfusion_end.IsEnabled = true;
                                    use_displayfusion.IsEnabled = true;

                                    //toggle ui
                                    bool displayfusiontogglestatus = AppSettings.Load<bool>("usedisplayfusion");
                                    if (displayfusiontogglestatus == true)
                                    {
                                        use_displayfusion.IsOn = true;

                                        //Dashboardgrid
                                        displayfusionstate_color.Background = new SolidColorBrush(Colors.Green);
                                        displayfusionstate_text.Text = "ENABLED";
                                        displayfusionbutton.Tag = "ENABLED";

                                    }
                                    else
                                    {
                                        use_displayfusion.IsOn = false;

                                        //Dashboardgrid
                                        displayfusionstate_color.Background = new SolidColorBrush(Colors.Orange);
                                        displayfusionstate_text.Text = "DISABLED";
                                        displayfusionbutton.Tag = "DISABLED";
                                    }

                                    // Parse JSON data
                                    if (!string.IsNullOrEmpty(monitorJson))
                                    {
                                        var monitorData = JObject.Parse(monitorJson);
                                        string makeModel = monitorData["UniqueID"]?["MakeModel"]?.ToString();
                                        string dpiScalingPercent = monitorData["DpiScalingPercent"]?.ToString();

                                    }

                                    //Load Profile Name Data
                                    string displayfusionstartprofile = AppSettings.Load<string>("usedisplayfusion_start");
                                    string displayfusionendprofile = AppSettings.Load<string>("usedisplayfusion_end");
                                    displayfusion_start.Text = displayfusionstartprofile;
                                    displayfusion_end.Text = displayfusionendprofile;

                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("The registry path was not found. or program is not installed");
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
                Console.WriteLine($"Error while reading the registry: {ex.Message}");

                //Displayfusion not installed or error
            }
            #endregion displayfusion
            #region gcmwallpaper
            try
            {
                bool gcmwallpaper = AppSettings.Load<bool>("gcmwallpaper");
                if (gcmwallpaper == true)
                {
                    //Dashboardgrid
                    gcmwallpaperbutton.Tag = "ENABLED";
                    gcmwallpaperstate_color.Background = new SolidColorBrush(Colors.Green);
                    gcmwallpaperstate_text.Text = "ENABLED";
                   

                    use_wallpaper.IsOn = true;
                    text_install_state_wallpaper.Text = "ACTIVATED";
                    border_install_state_wallpaper.Background = new SolidColorBrush(Colors.Green);
                    //text
                    string wallpaperpath = AppSettings.Load<string>("gcmwallpaperpath");
                    wallpaper_path.Text = wallpaperpath;
                    


                }
                else
                {
                    //Dashboardgrid
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
                Console.WriteLine("wallpaper gui error");
            }
            #endregion gcmwallpaper
            #region StartupVideo
            try
            {
                bool usestartupvideo = AppSettings.Load<bool>("usestartupvideo");
                if (usestartupvideo == true)
                {
                    text_install_state_Startup_Video.Text = "ACTIVATED";
                    border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Green);
                    use_startup_video.IsOn = true;

                    //Dashboardgrid
                    startupvideobutton.Tag = "ENABLED";
                    startupvideostate_color.Background = new SolidColorBrush(Colors.Green);
                    startupvideostate_text.Text = "ENABLED";
                }
                else
                {
                    text_install_state_Startup_Video.Text = "DISABLED";
                    border_install_state_Startup_Video.Background = new SolidColorBrush(Colors.Brown);
                    use_startup_video.IsOn = false;

                    //Dashboardgrid
                    startupvideobutton.Tag = "DISABLED";
                    startupvideostate_color.Background = new SolidColorBrush(Colors.Orange);
                    startupvideostate_text.Text = "DISABLED";
                }
                string startupvideo_path = AppSettings.Load<string>("startupvideo_path");
                if (startupvideo_path == "")
                {
                    startupvideo_path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets\\GCM_Startup_Video.webm");
                    AppSettings.Save("startupvideo_path", startupvideo_path);
                }
                textbox_startupvideo_path.Text = startupvideo_path;

                if (AppSettings.Load<bool>("usesteamstartupvideo") == true)
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
            catch { }

            #endregion StartupVideo
            #region deckyloader
            try
            {
                // Get the user's home directory
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                // Construct the full path to PluginLoader.exe
                string homebrewPath = Path.Combine(userHome, "homebrew");
                string deckyloaderExePath = Path.Combine(homebrewPath, "services", "PluginLoader.exe");

                if (File.Exists(deckyloaderExePath))
                {
                    text_install_state_decky_loader.Text = "INSTALLED";
                    border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Green);
                    use_decky_loader.IsEnabled = true;
                    //toggle ui
                    bool deckyloadertogglestatus = AppSettings.Load<bool>("usedeckyloader");
                    if (deckyloadertogglestatus == true)
                    {
                        use_decky_loader.IsOn = true;

                        //Dashboardgrid
                        deckyloaderbutton.Tag = "ENABLED";
                        deckyloaderstate_color.Background = new SolidColorBrush(Colors.Green);
                        deckyloaderstate_text.Text = "ENABLED";
                    }
                    else
                    {
                        use_decky_loader.IsOn = false;

                        //Dashboardgrid
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

                    //Dashboardgrid
                    deckyloaderbutton.Tag = "DISABLED";
                    deckyloaderstate_color.Background = new SolidColorBrush(Colors.Orange);
                    deckyloaderstate_text.Text = "DISABLED";
                }
            }
            catch
            {
            }
            #endregion deckyloader
            #region preloadlist
            try
            {
                bool usepreloadlist = AppSettings.Load<bool>("usepreloadlist");
                if (usepreloadlist == true)
                {
                    text_install_state_preloadlist.Text = "ACTIVATED";
                    border_install_state_preloadlist.Background = new SolidColorBrush(Colors.Green);
                    use_preloadlist.IsOn = true;
                    string preloadListFilePath = AppSettings.Load<string>("prealoadlistpath");
                    preloadlist_path.Text = preloadListFilePath;

                    
                    //Dashboardgrid
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

                    //Dashboardgrid
                    preloadlistbutton.Tag = "DISABLED";
                    preloadliststate_color.Background = new SolidColorBrush(Colors.Orange);
                    preloadliststate_text.Text = "DISABLED";
                }


            }
            catch
            {
                Console.WriteLine("preloadlist gui error");
            }

            #endregion preloadlist
            #region winpart
            try
            {
                bool usewinpart = AppSettings.Load<bool>("usewinpart");
                if (usewinpart == true)
                {
                    text_install_state_winpart.Text = "ACTIVATED";
                    border_install_state_winpart.Background = new SolidColorBrush(Colors.Green);
                    use_winpart.IsOn = true;


                    //Dashboardgrid
                    winpartbutton.Tag = "ENABLED";
                    winpartstate_color.Background = new SolidColorBrush(Colors.Green);
                    winpartstate_text.Text = "ENABLED";


                    bool usewinpartstartapps = AppSettings.Load<bool>("usewinpartstartapps");
                    try
                    {
                        if(usewinpartstartapps == true)
                        {

                            use_winpart_startapps.IsOn = true;
                        }
                        else
                        {
                            use_winpart_startapps.IsOn = false;
                        }

                    }
                    catch
                    {
                        use_winpart_startapps.IsOn = false;
                    }

                }
                else
                {
                    text_install_state_winpart.Text = "DISABLED";
                    border_install_state_winpart.Background = new SolidColorBrush(Colors.Brown);
                    use_winpart.IsOn = false;

                    //Dashboardgrid
                    winpartbutton.Tag = "DISABLED";
                    winpartstate_color.Background = new SolidColorBrush(Colors.Orange);
                    winpartstate_text.Text = "DISABLED";
                }

            }
            catch
            {
                Console.WriteLine("preloadlist gui error");
                AppSettings.Save("usewinpart", false);
                AppSettings.Save("usewinpartstartapps", false);
            }

            #endregion winpart
        }
        #endregion update ui
        #endregion code functions
        #region function
        #region preload
        private void btn_create_or_open_preloadlist_Click(object sender, RoutedEventArgs e)
        {
            // Get the AppData\Roaming folder path
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Build the gcmsettings folder path
            string gcmFolderPath = Path.Combine(appDataPath, "gcmsettings");

            // Ensure the folder exists
            if (!Directory.Exists(gcmFolderPath))
            {
                Directory.CreateDirectory(gcmFolderPath);
            }

            // Define the full path to the preloadlist.txt file
            string preloadListFilePath = Path.Combine(gcmFolderPath, "preloadlist.txt");

            // Check if the file exists, create if it doesn't
            if (!File.Exists(preloadListFilePath))
            {
                // Create the file and write an initial line
                File.WriteAllText(preloadListFilePath, " ");
                //Initial txt in list path
                AppSettings.Save("prealoadlistpath", preloadListFilePath);
                preloadlist_path.Text = preloadListFilePath;
            }

            // Open the file with the default associated editor
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = preloadListFilePath,

                    UseShellExecute = true
                });

                AppSettings.Save("prealoadlistpath", preloadListFilePath);
                preloadlist_path.Text = preloadListFilePath;
            }
            catch (Exception ex)
            {
                // Optional: show an error message or log it
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

        }

        private void use_preloadlist_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (use_preloadlist.IsOn == true)
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
                Console.WriteLine("problem with preloadlist integration");
            }
        }
        #endregion preload
        #region preadio
      
        private void preaudio_start_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComboBox comboBox = sender as ComboBox;

                if (comboBox != null && comboBox.SelectedItem != null)
                {
                    // devicename
                    string selectedDeviceName = comboBox.SelectedItem.ToString();
                    string cleanedDeviceName = selectedDeviceName.Split('(')[0].Trim();

                    // Speaker-ID
                    AppSettings.Save("preaudiostart", cleanedDeviceName);
                    Console.WriteLine($"Saved Device ID: {cleanedDeviceName}");
                    NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedDeviceName}\"");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving selected device: {ex.Message}");
            }
        }

        private void preaudio_end_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ComboBox comboBox = sender as ComboBox;

                if (comboBox != null && comboBox.SelectedItem != null)
                {
                    // devicename
                    string selectedDeviceName = comboBox.SelectedItem.ToString();
                    string cleanedDeviceName = selectedDeviceName.Split('(')[0].Trim();

                    // device-ID
                    AppSettings.Save("preaudioend", cleanedDeviceName);
                    Console.WriteLine($"Saved Device ID: {cleanedDeviceName}");
                    NirCmdUtil.NirCmdHelper.ExecuteCommand($"setdefaultsounddevice \"{cleanedDeviceName}\"");

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving selected device: {ex.Message}");
            }
        }

        private void use_preaudio_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                PopulateAudioDevices(false, true);

                if (use_preaudio.IsOn == true)
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
                Console.WriteLine("problem with speaker integration");
            }
        }
       

        private void PopulateAudioDevices(bool discord, bool preaudio)
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();

                // Clear existing items and reset the device map

                preaudio_end.Items.Clear();
                preaudio_start.Items.Clear();

                // Get playback devices
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, NAudio.CoreAudioApi.DeviceState.Active))
                {
                    if (discord == true)
                    {
                        // Geräte-Namen zur ComboBox hinzufügen
                    }
                    else if (preaudio == true)
                    {
                        preaudio_end.Items.Add(device.FriendlyName);
                        preaudio_start.Items.Add(device.FriendlyName);
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while populating audio devices: {ex.Message}");
            }
        }
        #endregion preaudio
        #region deckyloader
        private async void button_install_decky_loader_Click(object sender, RoutedEventArgs e)
        {
            // 1. Retrieve the latest release information from GitHub API
            string latestReleaseApiUrl = "https://api.github.com/repos/ACCESS-DENIIED/Decky-Loader-For-Windows/releases/latest";
            using (HttpClient httpClient = new HttpClient())
            {
                // GitHub API requires a valid User-Agent header
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                string releaseJson;
                try
                {
                    releaseJson = await httpClient.GetStringAsync(latestReleaseApiUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error retrieving the latest release info: " + ex.Message);
                    return;
                }

                // 2. Parse the JSON to extract the tag name and select the MSI asset
                JsonDocument jsonDoc = JsonDocument.Parse(releaseJson);
                JsonElement root = jsonDoc.RootElement;
                string tagName = root.GetProperty("tag_name").GetString();
                Console.WriteLine("Latest release version: " + tagName);

                // Get the assets array from the JSON
                JsonElement assets = root.GetProperty("assets");
                JsonElement? msiAsset = null;
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString();
                    // Select asset if it ends with .msi (case-insensitive)
                    if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        msiAsset = asset;
                        break;
                    }
                }

                if (msiAsset == null)
                {
                    Console.WriteLine("No Zip asset found in the latest release.");
                    return;
                }

                string selectedAssetName = msiAsset.Value.GetProperty("name").GetString();
                string downloadUrl = msiAsset.Value.GetProperty("browser_download_url").GetString();

                Console.WriteLine("Selected asset: " + selectedAssetName);
                Console.WriteLine("Download URL: " + downloadUrl);

                // 3. Determine the destination folder (create a custom subfolder in the Downloads directory)
                string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "deckyloader");
                if (!Directory.Exists(downloadsFolder))
                    Directory.CreateDirectory(downloadsFolder);

                string destinationPath = Path.Combine(downloadsFolder, selectedAssetName);

                // 4. Download the MSI file using Flurl.Http (the async download is awaited)
                try
                {
                    Console.WriteLine("Starting download of the MSI asset...");
                    await downloadUrl.DownloadFileAsync(downloadsFolder, selectedAssetName);
                    Console.WriteLine("Download complete. File saved at:");
                    Console.WriteLine(destinationPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Download error: " + ex.Message);
                    return;
                }

                // 5. Open the folder in File Explorer so the user can directly access the downloaded zip file
                try
                {
                    

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{downloadsFolder}\"",
                        UseShellExecute = true
                    });
                    Console.WriteLine("Folder opened in File Explorer.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error opening the folder: " + ex.Message);
                }
            }
        }
        private void use_decky_loader_Toggled_1(object sender, RoutedEventArgs e)
        {
            if (use_decky_loader.IsOn == true)
            {
                AppSettings.Save("usedeckyloader", true);
                text_install_state_decky_loader.Text = "INSTALLED";
                border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Green);
            }
            else
            {
                AppSettings.Save("usedeckyloader", false);
                text_install_state_decky_loader.Text = "NOT INSTALLED";
                border_install_state_decky_loader.Background = new SolidColorBrush(Colors.Brown);
            }
        }
        #endregion Deckyloader
        #region StartupVideo
        private void textbox_startupvideo_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                AppSettings.Save("startupvideo_path", textbox_startupvideo_path.Text);
            }
            catch { }
        }

        private void use_startup_video_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_startup_video.IsOn == true)
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
            pichstartupvideopath.IsEnabled = true;
        }

        private void UseSteamStartupVideoCheckBox_Checked(object sender, RoutedEventArgs e)
        {

            // Save the checkbox state in the settings
            AppSettings.Save("usesteamstartupvideo", true);

            // Enable video injection
            Injectstartupvideo_button.IsEnabled = true;
            textbox_select_startupvideo_path.Visibility = Visibility.Collapsed; // Hide the text field

        }

        private void UseSteamStartupVideoCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

            // Save the checkbox state in the settings
            AppSettings.Save("usesteamstartupvideo", false);

            // Disable video injection
            Injectstartupvideo_button.IsEnabled = false;
            textbox_select_startupvideo_path.Visibility = Visibility.Visible; // Show the text field

        }

        private void Injectstartupvideo_button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Injectstartupvideo_Text.Text = "";
                Injectstartupvideo_Text.Visibility = Visibility.Visible;
                Injectstartupvideo_ProgressBar.Visibility = Visibility.Visible;
                Injectstartupvideo_ProgressBar.Value = 0;
                Debug.WriteLine("Start of Injectstartupvideo_button_Click function");

                Injectstartupvideo_button.IsEnabled = false; // Disable the button

                string videoPath = AppSettings.Load<string>("startupvideo_path");
                Debug.WriteLine($"Video path retrieved: {videoPath}");

                if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
                {
                    Injectstartupvideo_Text.Text = "No valid video file selected.";
                    Injectstartupvideo_ProgressBar.Visibility = Visibility.Collapsed;
                    throw new Exception("No valid video file selected.");
                }

                // Check if the source file is already WebM
                if (Path.GetExtension(videoPath).ToLower() != ".webm")
                {
                    Injectstartupvideo_Text.Text = "The selected file is not in WebM format.";
                    Injectstartupvideo_ProgressBar.Visibility = Visibility.Collapsed;
                    Injectstartupvideo_ProgressBar.Value = 0;
                    throw new Exception("The selected file is not in WebM format.");
                }

                string steamPath = AppSettings.Load<string>("steamlauncherpath");
                if (steamPath.EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase))
                {
                    steamPath = Path.GetDirectoryName(steamPath);
                }

                string outputPath = Path.Combine(steamPath, "steamui", "movies", "GCM_vid.webm");

                // Check and create the folder if needed
                string moviesFolder = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(moviesFolder))
                {
                    Directory.CreateDirectory(moviesFolder);
                }

                // Copy the WebM file
                Injectstartupvideo_ProgressBar.Value = 50;
                File.Copy(videoPath, outputPath, true);
                Debug.WriteLine("File copy complete");

                Injectstartupvideo_ProgressBar.Visibility = Visibility.Visible;
                Injectstartupvideo_ProgressBar.Value = 100;
                Injectstartupvideo_Text.Text = "Video copied successfully.";
                Injectstartupvideo_button.IsEnabled = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: {ex.Message}");
                Injectstartupvideo_ProgressBar.Visibility = Visibility.Collapsed;
                Injectstartupvideo_Text.Text = "Error: " + ex.Message;
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
            catch { }
        }

        #endregion StartupVideo
        #region displayfusion
        private void use_displayfusion_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_displayfusion.IsOn == true)
            {
                AppSettings.Save("usedisplayfusion", true);
            }
            else
            {
                AppSettings.Save("usedisplayfusion", false);
            }
        }
        private void button_uninstall_displayfusion_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo("ms-settings:appsfeatures")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening Windows settings: " + ex.Message);
            }
        }
        private void button_install_displayfusion_Click_2(object sender, RoutedEventArgs e)
        {
            // URL to open
            string url = "https://www.displayfusion.com/download/";

            try
            {
                // Open the URL in the default web browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true // Ensures it opens in the default browser
                });

                Console.WriteLine("Webpage launched successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching webpage: {ex.Message}");
            }
        }
        private void displayfusion_start_TextChanged(object sender, TextChangedEventArgs e)
        {
            string startprofile = displayfusion_start.Text;
            AppSettings.Save("usedisplayfusion_start", startprofile);
        }
        private void displayfusion_end_TextChanged(object sender, TextChangedEventArgs e)
        {
            string endprofile = displayfusion_end.Text;
            AppSettings.Save("usedisplayfusion_end", endprofile);
        }
        #endregion displayfusion
        #region wallpaper GCM
        private void wallpaper_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            string wallpaperpath = wallpaper_path.Text;
            AppSettings.Save("gcmwallpaperpath", wallpaperpath);
        }
        private void use_wallpaper_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_wallpaper.IsOn == true)
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
        #endregion wallpaper GCM
        #region csloader
        private void button_uninstall_cssloader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo("ms-settings:appsfeatures")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening Windows settings: " + ex.Message);
            }
        }

        private async void button_install_cssloader_Click(object sender, RoutedEventArgs e)
        {

            // 1. Retrieve the latest release information from GitHub API
            string latestReleaseApiUrl = "https://api.github.com/repos/DeckThemes/CSSLoader-Desktop/releases/latest";
            using (HttpClient httpClient = new HttpClient())
            {
                // GitHub API requires a valid User-Agent header
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                string releaseJson;
                try
                {
                    releaseJson = await httpClient.GetStringAsync(latestReleaseApiUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error retrieving the latest release info: " + ex.Message);
                    return;
                }

                // 2. Parse the JSON to extract the tag name and select the MSI asset
                JsonDocument jsonDoc = JsonDocument.Parse(releaseJson);
                JsonElement root = jsonDoc.RootElement;
                string tagName = root.GetProperty("tag_name").GetString();
                Console.WriteLine("Latest release version: " + tagName);

                // Get the assets array from the JSON
                JsonElement assets = root.GetProperty("assets");
                JsonElement? msiAsset = null;
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string assetName = asset.GetProperty("name").GetString();
                    // Select asset if it ends with .msi (case-insensitive)
                    if (assetName.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        msiAsset = asset;
                        break;
                    }
                }

                if (msiAsset == null)
                {
                    Console.WriteLine("No MSI asset found in the latest release.");
                    return;
                }

                string selectedAssetName = msiAsset.Value.GetProperty("name").GetString();
                string downloadUrl = msiAsset.Value.GetProperty("browser_download_url").GetString();

                Console.WriteLine("Selected asset: " + selectedAssetName);
                Console.WriteLine("Download URL: " + downloadUrl);

                // 3. Determine the destination folder (create a custom subfolder in the Downloads directory)
                string downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "DownloadedInstaller");
                if (!Directory.Exists(downloadsFolder))
                    Directory.CreateDirectory(downloadsFolder);

                string destinationPath = Path.Combine(downloadsFolder, selectedAssetName);

                // 4. Download the MSI file using Flurl.Http (the async download is awaited)
                try
                {
                    Console.WriteLine("Starting download of the MSI asset...");
                    await downloadUrl.DownloadFileAsync(downloadsFolder, selectedAssetName);
                    Console.WriteLine("Download complete. File saved at:");
                    Console.WriteLine(destinationPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Download error: " + ex.Message);
                    return;
                }

                // 5. Open the folder in File Explorer so the user can directly access the downloaded MSI file
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{downloadsFolder}\"",
                        UseShellExecute = true
                    });
                    Console.WriteLine("Folder opened in File Explorer.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error opening the folder: " + ex.Message);
                }
            }

        }

        private void use_cssloader_Toggled(object sender, RoutedEventArgs e)
        {


            if (use_cssloader.IsOn == true)
            {
                AppSettings.Save("usecssloader", true);
            }
            else
            {
                AppSettings.Save("usecssloader", false);
            }
        }
        #endregion csloader
        #region joyxoff
        private void button_uninstall_joyxoff_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new ProcessStartInfo("ms-settings:appsfeatures")
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening Windows settings: " + ex.Message);
            }
        }
        private async void button_install_joyxoff_Click(object sender, RoutedEventArgs e)
        {
            // 1. Dynamically determine the version from the download page
            string baseDownloadUrl = "https://joyxoff.com/download.php?culture=en";
            string pageContent = "";
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                    pageContent = wc.DownloadString(baseDownloadUrl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error retrieving the page: " + ex.Message);
                return;
            }

            // Regex searches for a link that contains the version pattern, e.g. ?culture=en&version=3.63.10.7
            string pattern = @"download\.php\?culture=en&version=([\d\.]+)";
            var match = Regex.Match(pageContent, pattern);
            string version = match.Success ? match.Groups[1].Value : "3.63.10.7";
            Console.WriteLine("Found version: " + version);

            // 2. Build the download URL
            string downloadUrl = $"https://joyxoff.com/download.php?culture=en&version={version}";
            Console.WriteLine("Download URL: " + downloadUrl);

            // 3. Destination folder: create a custom subfolder in the Downloads directory
            string downloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "joyxoffInstaller");
            if (!Directory.Exists(downloadFolder))
                Directory.CreateDirectory(downloadFolder);

            string fileName = "joyxoff.rar";
            string destinationPath = Path.Combine(downloadFolder, fileName);

            // 4. Download the file using Flurl.Http (the async download is executed synchronously)
            try
            {
                Console.WriteLine("Starting download with Flurl.Http ...");
                downloadUrl.DownloadFileAsync(downloadFolder, fileName).GetAwaiter().GetResult();
                Console.WriteLine("Download complete. File saved at:");
                Console.WriteLine(destinationPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Download error: " + ex.Message);
                return;
            }

            // 5. Open the folder in File Explorer so the user can see the downloaded RAR file
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{downloadFolder}\"",
                    UseShellExecute = true
                });
                Console.WriteLine("Folder opened in File Explorer.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening the folder: " + ex.Message);
            }
        }

        private void use_joyxoff_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_joyxoff.IsOn == true)
            {
                AppSettings.Save("usejoyxoff", true);
            }
            else
            {
                AppSettings.Save("usejoyxoff", false);
            }

        }
        #endregion joyxoff
        #region winpart
        private void use_winpart_Toggled(object sender, RoutedEventArgs e)
        {

            if (use_winpart.IsOn == true)
            {
                AppSettings.Save("usewinpart", true);
            }
            else
            {
                AppSettings.Save("usewinpart", false);
                AppSettings.Save("usewinpartstartapps", false);
            }
        }

        private void use_winpart_startapps_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_winpart_startapps.IsOn == true)
            {
                AppSettings.Save("usewinpartstartapps", true);
            }
            else
            {
                AppSettings.Save("usewinpartstartapps", false);
            }
        }
        #endregion winpart
        #endregion function

        #region codebehind Function Dashboard
        #region helpers
        private void Openpanel(string panel)
        {
           
            // Erst alle Panels verstecken
            foreach (var child in DetailContentGrid.Children)
            {
                if (child is Grid grid)
                {
                    grid.Visibility = Visibility.Collapsed;
                   
                }
            }

            // Nur das angeklickte Panel sichtbar machen
            var selectedPanel = DetailContentGrid.FindName(panel) as Grid;
            if (selectedPanel != null)
            {
                
                selectedPanel.Visibility = Visibility.Visible;
            }

           

            // Detailansicht zentriert anzeigen
            PanelView.Visibility = Visibility.Visible;

            // Animation starten
            var storyboard = (Storyboard)this.Resources["ShowDetailPanelStoryboard"];
            storyboard.Begin();

            
            //collaps all
            filterbuttonall.Visibility = Visibility.Collapsed;
            filterbuttonenabled.Visibility = Visibility.Collapsed;
            filterbuttondisabled.Visibility = Visibility.Collapsed;
            SearchBox.Visibility = Visibility.Collapsed;
        }


        private void CloseDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            var storyboard = (Storyboard)this.Resources["HideDetailPanelStoryboard"];
            storyboard.Completed += (s, args) =>
            {
                PanelView.Visibility = Visibility.Collapsed;
            };
            //collaps all
            filterbuttonall.Visibility = Visibility.Visible;
            filterbuttonenabled.Visibility = Visibility.Visible;
            filterbuttondisabled.Visibility = Visibility.Visible;
            SearchBox.Visibility = Visibility.Visible;

            storyboard.Begin();
        }
        #endregion helpers
        #region searchbox
        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            FilterDashboardsearchButtons(SearchBox.Text);
        }
        #endregion searchbox
        private string? GetFirstTextBlockText(DependencyObject parent)
        {
            foreach (var child in FindVisualChildren<TextBlock>(parent))
            {
                if (!string.IsNullOrWhiteSpace(child.Text))
                    return child.Text;
            }
            return null;
        }

        private void cssloader_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("cssloader");
        }

        private void preloadlist_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("preloadlist");
        }

        private void gcmwallpaperbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("gcmwallpaper");
        }

        private void joyxoffbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("JoyxoffPanel");
        }

        private void deckyloaderbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("deckyloader");

        }

        private void preaudiobutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("preaudio");
        }

        private void displayfusionbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("displayfusion");
        }

        private void startupvideobutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("startupvideo");
        }

        private void winpartbutton_Click(object sender, RoutedEventArgs e)
        {
            Openpanel("winpart");
        }

        #endregion codebehind Function Dashboard

      
    }

    //region nircmd code
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
                // Determine the path to nircmd.exe in the current directory
                string nircmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nircmd.exe");

                if (!File.Exists(nircmdPath))
                {
                    throw new FileNotFoundException("nircmd.exe was not found in the current directory.");
                }

                // Configure ProcessStartInfo for nircmd
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

                    // Optionally capture output and error streams
                    string output = process.StandardOutput.ReadToEnd();
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


}
