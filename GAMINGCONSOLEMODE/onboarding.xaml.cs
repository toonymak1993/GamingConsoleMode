using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class Onboarding : Page
    {
        // UI Arrays für einfacheren Zugriff
        private Border[] _stepIndicators;
        private TextBlock[] _stepNumbers;
        private TextBlock[] _stepTexts;
        private FontIcon[] _stepArrows;

        // Farben
        private SolidColorBrush _accentBrush;
        private SolidColorBrush _neutralBrush;
        private SolidColorBrush _textActiveBrush;
        private SolidColorBrush _textInactiveBrush;

        private bool _isPageLoaded = false;

        public Onboarding()
        {
            this.InitializeComponent();
            this.Loaded += Onboarding_Loaded;

            // Farben initialisieren
            _accentBrush = Application.Current.Resources["SystemAccentColor"] as SolidColorBrush;

            // Fallback, falls Ressource nicht gefunden
            if (_accentBrush == null) _accentBrush = new SolidColorBrush(Colors.DodgerBlue);

            _neutralBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64)); // Dunkelgrau
            _textActiveBrush = new SolidColorBrush(Colors.White);
            _textInactiveBrush = new SolidColorBrush(Colors.Gray);
        }

        private void Onboarding_Loaded(object sender, RoutedEventArgs e)
        {
            // Referenzen zu den UI Elementen speichern (Jetzt 5 Schritte!)
            _stepIndicators = new Border[] { Step1Indicator, Step2Indicator, Step3Indicator, Step4Indicator, Step5Indicator };
            _stepNumbers = new TextBlock[] { Step1Number, Step2Number, Step3Number, Step4Number, Step5Number };
            _stepTexts = new TextBlock[] { Step1Text, Step2Text, Step3Text, Step4Text, Step5Text };
            // Pfeile zwischen den Schritten (1->2, 2->3, 3->4, 4->5 = 4 Pfeile)
            _stepArrows = new FontIcon[] { Arrow1, Arrow2, Arrow3, Arrow4 };

            // Initialen Status setzen
            UpdateStepperVisuals(0);

            try
            {
                // Einstellungen laden
                AutostartToggle.IsOn = AppSettings.Load<bool>("usewinpartstartapps");

                // API Key laden, falls schon vorhanden
                string savedKey = AppSettings.Load<string>("steamgriddb_api_key");
                if (!string.IsNullOrEmpty(savedKey))
                {
                    ApiKeyBox.Text = savedKey;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler beim Laden der Einstellungen: {ex.Message}");
            }

            _isPageLoaded = true;
        }

        private void OnboardingFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isPageLoaded) return;

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
                    // Aktiver Schritt
                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Opacity = 1.0;
                }
                else if (i < activeIndex)
                {
                    // Vergangener Schritt
                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush;
                    _stepTexts[i].Opacity = 0.7;
                }
                else
                {
                    // Zukünftiger Schritt
                    _stepIndicators[i].Background = _neutralBrush;
                    _stepNumbers[i].Foreground = _textInactiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush;
                    _stepTexts[i].Opacity = 0.5;
                }
            }

            // Pfeile einfärben
            for (int i = 0; i < _stepArrows.Length; i++)
            {
                if (i < activeIndex)
                    _stepArrows[i].Foreground = _accentBrush;
                else
                    _stepArrows[i].Foreground = _neutralBrush;
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

            // Navigationslogik
            if (this.Frame != null)
            {
                this.Frame.Navigate(typeof(launcher));
            }
            else
            {
                if (this.XamlRoot != null && this.XamlRoot.Content is Frame rootFrame)
                {
                    rootFrame.Navigate(typeof(launcher));
                }
                else
                {
                    Debug.WriteLine("Could not find the main Frame to navigate to Launcher.");
                }
            }
        }

        // Links und Buttons
        private void onboarding_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserLauncher.OpenUrlInBrowser("https://www.gameconsolemode.com/onboarding");
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserLauncher.OpenUrlInBrowser("https://discord.gg/FbjYDeEJce");
        }

        private void autologon_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserLauncher.OpenUrlInBrowser("https://download.sysinternals.com/files/AutoLogon.zip");
        }

        private void AutostartToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggleSwitch)
            {
                bool isNowOn = toggleSwitch.IsOn;
                Debug.WriteLine($"Autostart Management: {(isNowOn ? "ON" : "OFF")}");
                AppSettings.Save("usewinpartstartapps", isNowOn);
            }
        }

        // --- NEU: API Key Logic ---

        private void GetApiKeyButton_Click(object sender, RoutedEventArgs e)
        {
            // Link direkt zur API-Key Erstellung bei SteamGridDB
            WebBrowserLauncher.OpenUrlInBrowser("https://www.steamgriddb.com/profile/preferences/api");
        }

        private void ApiKeyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Speichert den Key sofort, wenn der Nutzer ihn eingibt/einfügt
            if (sender is TextBox tb)
            {
                string key = tb.Text.Trim();
                AppSettings.Save("steamgriddb_api_key", key);
            }
        }

        // Helper Klasse für Browser Aufrufe
        public static class WebBrowserLauncher
        {
            public static void OpenUrlInBrowser(string url)
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error opening URL: {ex.Message}");
                }
            }
        }
    }
}