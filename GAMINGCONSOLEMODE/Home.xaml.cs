using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Page = Microsoft.UI.Xaml.Controls.Page;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
  
    public sealed partial class Home : Page
    {  // Statische Referenz auf MainWindow

      

        public Home()
        {
            this.InitializeComponent();

            StartBounceAnimation();
         

        }
       

        private void StartBounceAnimation()
        {
            // Create the Bounce Animation
            DoubleAnimation bounceAnimation = new DoubleAnimation
            {
                From = 0,
                To = -20,
                Duration = new Duration(TimeSpan.FromMilliseconds(500)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            // Apply the Animation to the TranslateTransform
            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(bounceAnimation);
            Storyboard.SetTarget(bounceAnimation, IconBounceTransform); // IconBounceTransform from XAML
            Storyboard.SetTargetProperty(bounceAnimation, "Y"); // Animate the Y property of TranslateTransform
            storyboard.Begin();
        }

        static string exeFolder()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string folderPath = Path.GetDirectoryName(exePath);
            return folderPath;
        }

        private void Button_gcmplay_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(exeFolder(), "gcmloader.exe")));
        }

        private void wikibutton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void functionbutton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(startup));
        }

        private void wikibutton_Click_1(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/Kosnix/GameConsoleMode/wiki";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Console.WriteLine("The URL has been opened in your default browser.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening the URL: " + ex.Message);
            }
        }

        private void withreplace_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("replace", true);
        }

        private void withoutreplace_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Save("replace", false);
        }
    }

}




