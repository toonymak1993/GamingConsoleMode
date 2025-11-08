// In Onboarding.xaml.cs
using Microsoft.UI; // Für Colors
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Windows.System;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class Onboarding : Page
    {
       
        private Border[] _stepIndicators;
        private TextBlock[] _stepNumbers;
        private TextBlock[] _stepTexts;
        private FontIcon[] _stepArrows;

       
        private SolidColorBrush _accentBrush;
        private SolidColorBrush _neutralBrush;
        private SolidColorBrush _textActiveBrush;
        private SolidColorBrush _textInactiveBrush;

        
        private bool _isPageLoaded = false;
     

        public Onboarding()
        {
            this.InitializeComponent();
            this.Loaded += Onboarding_Loaded;

  

            
            _accentBrush = Application.Current.Resources["SystemAccentColor"] as SolidColorBrush;

        
            _neutralBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64)); 
            _textActiveBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
            _textInactiveBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        private void Onboarding_Loaded(object sender, RoutedEventArgs e)
        {
         
            _stepIndicators = new Border[] { Step1Indicator, Step2Indicator, Step3Indicator, Step4Indicator };
            _stepNumbers = new TextBlock[] { Step1Number, Step2Number, Step3Number, Step4Number };
            _stepTexts = new TextBlock[] { Step1Text, Step2Text, Step3Text, Step4Text };
            _stepArrows = new FontIcon[] { Arrow1, Arrow2, Arrow3 };

           
            UpdateStepperVisuals(0);

            try
            {

                AutostartToggle.IsOn = AppSettings.Load<bool>("usewinpartstartapps");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Laden der Toggle-Einstellungen: {ex.Message}");
            }

            _isPageLoaded = true;

        }


        private void OnboardingFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
           
            if (!_isPageLoaded)
            {
                return; 
            }

            int index = OnboardingFlipView.SelectedIndex;
            if (index == -1) return;

            UpdateStepperVisuals(index);
            UpdateNavButtons(index);
        }

       
        private void UpdateStepperVisuals(int activeIndex)
        {
            if (_stepIndicators == null) return; 


            for (int i = 0; i < _stepIndicators.Length; i++)
            {
                if (i == activeIndex)
                {

                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textActiveBrush;
                }
                else if (i < activeIndex)
                {
                   
                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush; 
                }
                else
                {
                
                    _stepIndicators[i].Background = _neutralBrush;
                    _stepNumbers[i].Foreground = _textInactiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush;
                }
            }

           
            for (int i = 0; i < _stepArrows.Length; i++)
            {
                
                if (i < activeIndex)
                {
                    _stepArrows[i].Foreground = _accentBrush;
                }
                else
                {
                    _stepArrows[i].Foreground = _neutralBrush;
                }
            }
        }

        
        private void UpdateNavButtons(int index)
        {
          
            BackButton.IsEnabled = (index > 0);

            if (index == OnboardingFlipView.Items.Count - 1)
            {
              
                NextButton.Visibility = Visibility.Collapsed;
                FinishButton.Visibility = Visibility.Visible;
            }
            else
            {
                
                NextButton.Visibility = Visibility.Visible;
                FinishButton.Visibility = Visibility.Collapsed;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (OnboardingFlipView.SelectedIndex > 0)
            {
                OnboardingFlipView.SelectedIndex--;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (OnboardingFlipView.SelectedIndex < OnboardingFlipView.Items.Count - 1)
            {
                OnboardingFlipView.SelectedIndex++;
            }
        }

        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {

            Debug.WriteLine("Onboarding Finished! Navigating to Launcher...");

            if (this.Frame != null)
            {
               
                this.Frame.Navigate(typeof(launcher));
            }
            else
            {
               
                if (this.XamlRoot != null && this.XamlRoot.Content is Frame rootFrame)
                {
                    rootFrame.Navigate(typeof(Launcher));
                }
                else
                {
                    Debug.WriteLine("Could not find the main Frame to navigate to Launcher.");
                }
            }
        }

        private void onboarding_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserLauncher.OpenUrlInBrowser("https://www.gameconsolemode.com/onboarding");
        }


        public class WebBrowserLauncher
        {
           
            public static void OpenUrlInBrowser(string url)
            {
                try
                {
                    // Create a ProcessStartInfo object
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true // IMPORTANT: UseShellExecute must be true to open URLs in the default browser
                    };

                    // Start the process (which will be the default browser)
                    Process.Start(psi);

                    Console.WriteLine($"Successfully requested to open URL: {url}");
                }
                catch (Win32Exception ex)
                {
                    // Handle potential errors, e.g., no default browser is set or protocol is unknown
                    Console.WriteLine($"Error opening URL (Win32Exception): {ex.Message}");
                    // Consider showing a user-friendly error message here
                }
                catch (Exception ex)
                {
                    // Handle other potential errors
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}");
                    // Consider showing a user-friendly error message here
                }
            }
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserLauncher.OpenUrlInBrowser("https://discord.gg/FbjYDeEJce");
        }

        private void AutostartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                bool isNowOn = toggleSwitch.IsOn;
                if (isNowOn)
                {
                    // The user turned it ON
                    Debug.WriteLine("Autostart Management: ON");
                    AppSettings.Save("usewinpartstartapps", true);
                }
                else
                {
                    // The user turned it OFF
                    Debug.WriteLine("Autostart Management: OFF");
                    AppSettings.Save("usewinpartstartapps", false);
                }
            }
        }

        private void autologon_Click(object sender, RoutedEventArgs e)
        {
            // This button provides a direct download link to the Sysinternals AutoLogon tool.
            try
            {
                string url = "https://download.sysinternals.com/files/AutoLogon.zip";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to open AutoLogon download link: " + ex.Message);
            }
        }
    }
}