using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using System;
using WinRT.Interop;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.UI.Xaml.Media;

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
           initialui();
        }

        #region methodes

        // Button for SplitView to open/close
        private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
        {
            splitView.IsPaneOpen = !splitView.IsPaneOpen;
        }


        private void initialui()
        {

            try
            {
                #region launcher
                string steamlauncherpath = AppSettings.Load<string>("steamlauncherpath");
                textbox_steam_path.Text = steamlauncherpath;

                string playnitelauncherpath = AppSettings.Load<string>("playnitelauncherpath");
                textbox_playnite_path.Text = playnitelauncherpath;

                string customlauncherpath = AppSettings.Load<string>("customlauncherpath");
                textbox_custom_path.Text = customlauncherpath;
            }
            catch
            {

            }
            

            string launcher = AppSettings.Load<string>("launcher");
            switch (launcher)
            {
                case "steam":
                    use_steam_bp.IsOn = true;
                        use_playnite.IsOn = false;
                    use_custom.IsOn = false;
                    break;

                case "playnite":
                    use_playnite.IsOn = true;
                    use_steam_bp.IsOn = false;
                    use_custom.IsOn = false;
                    break;

                case "custom":
                    use_custom.IsOn = true;
                    use_playnite.IsOn = false;
                    use_steam_bp.IsOn = false;
                    break;

                default:
                    Console.WriteLine("Invalid launcher. Defaulting to Custom.");
                    launcher = "steam";
                    AppSettings.Save("launcher", launcher);

                    use_steam_bp.IsOn = true;
                    use_playnite.IsOn = false;
                    use_custom.IsOn = false;

                    break;
            }

        }

        private string getexe()
        {
            // File Picker
            string file = FilePicker.ShowDialog(
                "C:\\",                            // start directory
                new string[] { "exe" },            // file types
                "Executable Files",                // filter name
                "Select an EXE File"               // dialogue title
            );

            if (!string.IsNullOrEmpty(file))
            {
                // Get Exe Path

                return file;
            }
            else
            {
                // break
                return "none";

            }
        }

        private  async Task checkLauncherActivatedAsync()
        {
            if(use_steam_bp.IsOn == false & use_playnite.IsOn == false & use_custom.IsOn == false)
            {
                //messagebox
                var dialog = new ContentDialog
                {
                    Title = "Information",
                    Content = "Please select at least one launcher. The default launcher will now be set",
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot // IMPORTANT: Links the dialog to the current window


                };

                await dialog.ShowAsync();

                AppSettings.Save("launcher", "steam");
                //ui 
                initialui();
            }


        }

        #endregion launcher



        #endregion methodes

        #region events

        #region Steam
         private void textbox_steam_path_TextChanged(object sender, TextChangedEventArgs e)
                {
                    AppSettings.Save("steamlauncherpath", textbox_steam_path.Text);
                    //ui 
                    initialui();
                }
        
        private async void use_steam_bp_Toggled(object sender, RoutedEventArgs e)
        {
            if (use_steam_bp.IsOn == true)
            {
                AppSettings.Save("launcher", "steam");
                //ui 
                initialui();
                SteamPanel.Visibility = Visibility.Visible;
                PlaynitePanel.Visibility = Visibility.Collapsed;
                CustomPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
               await  checkLauncherActivatedAsync();
               SteamPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void pichsteampath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = getexe();

            if (exepath == "none")
            {
                return;
            }

            string expectedFileName = "steam.exe"; // Expected file name
            string selectedFile = System.IO.Path.GetFileName(exepath);

            if (selectedFile.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                // Save the path
                AppSettings.Save("steamlauncherpath", exepath);
                // Update UI
                initialui();
            }
            else
            {
                MessageBox.Show($"Please select the correct file: {expectedFileName}", "Invalid File");
            }
        }


        #endregion Steam
        #region Playnite

        private void pichplaynitepath_Click(object sender, RoutedEventArgs e)
        {
            string exepath = getexe();

            if (exepath == "none")
            {
                return;
            }

            string expectedFileName = "Playnite.FullscreenApp.exe"; // Expected file name
            string selectedFile = System.IO.Path.GetFileName(exepath);

            if (selectedFile.Equals(expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                // Save the path
                AppSettings.Save("playnitelauncherpath", exepath);
                // Update UI
                initialui();
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
                //ui 
                initialui();
                SteamPanel.Visibility = Visibility.Collapsed;
                PlaynitePanel.Visibility = Visibility.Visible;
                CustomPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                await checkLauncherActivatedAsync();
                PlaynitePanel.Visibility = Visibility.Collapsed;
            }

        }

        private void textbox_playnite_path_TextChanged(object sender, TextChangedEventArgs e)
        {
                AppSettings.Save("playnitelauncherpath", textbox_playnite_path.Text);
                //ui 
                initialui();    
        }

        #endregion Playnite

        #region CustomLauncher

        private void pichcustompath_Click(object sender, RoutedEventArgs e)
                {
                    string exepath = getexe();

                    if (exepath == "none")
                    {

                    }
                    else
                    {
                        // Save Path
                        AppSettings.Save("customlauncherpath", exepath);
                        //ui 
                        initialui();
                    }

                }

        private async void use_custom_Toggled(object sender, RoutedEventArgs e)
                {
                    if (use_custom.IsOn == true)
                    {
                        AppSettings.Save("launcher", "custom");
                        //ui 
                        initialui();
                        SteamPanel.Visibility = Visibility.Collapsed;
                        PlaynitePanel.Visibility = Visibility.Collapsed;
                        CustomPanel.Visibility = Visibility.Visible;
            }
                    else
                    {
                        await checkLauncherActivatedAsync();
                        CustomPanel.Visibility = Visibility.Collapsed;
            }
                }

        private void textbox_custom_path_TextChanged(object sender, TextChangedEventArgs e)
        {
            AppSettings.Save("customlauncherpath", textbox_custom_path.Text);
            //ui 
            initialui();
        }

        #endregion CustomLauncher




        #endregion events


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
    public static class FilePicker
        {

            [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool GetOpenFileName(ref OpenFileName ofn);

            // usage: string filename = FilePicker.ShowDialog("C:\\", new string[] { "png", "jpeg", "jpg" }, "Image Files", "Select an Image File...");
            public static string ShowDialog(string startingDirectory, string[] filters, string filterName, string dialogTitle)
            {
                var ofn = new OpenFileName();
                ofn.lStructSize = Marshal.SizeOf(ofn);

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
                if (GetOpenFileName(ref ofn))
                    return ofn.lpstrFile;
                return string.Empty;
            }
        }
    }
}

