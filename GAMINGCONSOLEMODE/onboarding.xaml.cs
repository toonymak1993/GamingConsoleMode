// In Onboarding.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI; // Für Colors
using System.Diagnostics;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class Onboarding : Page
    {
        // Arrays für einen einfachen Zugriff auf die UI-Elemente des Steppers
        private Border[] _stepIndicators;
        private TextBlock[] _stepNumbers;
        private TextBlock[] _stepTexts;
        private FontIcon[] _stepArrows;

        // Farben für die verschiedenen Zustände
        private SolidColorBrush _accentBrush;
        private SolidColorBrush _neutralBrush;
        private SolidColorBrush _textActiveBrush;
        private SolidColorBrush _textInactiveBrush;

        // ### START DER KORREKTUR ###
        // Dieser Schalter verhindert, dass der Code ausgeführt wird, bevor die Seite geladen ist
        private bool _isPageLoaded = false;
        // ### ENDE DER KORREKTUR ###

        public Onboarding()
        {
            this.InitializeComponent();
            this.Loaded += Onboarding_Loaded;

            // Definiere die Farben. Wir holen die Akzentfarbe aus den App-Ressourcen.
            _accentBrush = Application.Current.Resources["SystemAccentColor"] as SolidColorBrush;

            // Wir verwenden die voll qualifizierten Namen, um den Fehler zu beheben:
            _neutralBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64)); // Dunkelgrau
            _textActiveBrush = new SolidColorBrush(Microsoft.UI.Colors.White);
            _textInactiveBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }

        private void Onboarding_Loaded(object sender, RoutedEventArgs e)
        {
            // Fülle die Arrays mit den UI-Elementen aus dem XAML
            _stepIndicators = new Border[] { Step1Indicator, Step2Indicator, Step3Indicator };
            _stepNumbers = new TextBlock[] { Step1Number, Step2Number, Step3Number };
            _stepTexts = new TextBlock[] { Step1Text, Step2Text, Step3Text };
            _stepArrows = new FontIcon[] { Arrow1, Arrow2 };

            // Setze den visuellen Startzustand (Schritt 1 ist aktiv)
            UpdateStepperVisuals(0);

            // ### START DER KORREKTUR ###
            // Jetzt, da alles geladen ist, setzen wir den Schalter auf true
            _isPageLoaded = true;
            // ### ENDE DER KORREKTUR ###
        }

        /// <summary>
        /// Wird jedes Mal aufgerufen, wenn die Seite im FlipView wechselt.
        /// </summary>
        private void OnboardingFlipView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ### START DER KORREKTUR ###
            // Führe den Code nur aus, wenn die Seite geladen ist und der BackButton nicht mehr null sein kann.
            if (!_isPageLoaded)
            {
                return; // Abbrechen, wenn die Seite noch nicht bereit ist
            }
            // ### ENDE DER KORREKTUR ###

            int index = OnboardingFlipView.SelectedIndex;
            if (index == -1) return;

            UpdateStepperVisuals(index);
            UpdateNavButtons(index);
        }

        /// <summary>
        /// Aktualisiert die Farben der Stepper-Kopfzeile basierend auf dem aktiven Index.
        /// </summary>
        private void UpdateStepperVisuals(int activeIndex)
        {
            if (_stepIndicators == null) return; // Verhindert Fehler, falls noch nicht geladen

            // Alle Schritte durchlaufen
            for (int i = 0; i < _stepIndicators.Length; i++)
            {
                if (i == activeIndex)
                {
                    // Aktueller Schritt: Vollständige Akzentfarbe
                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textActiveBrush;
                }
                else if (i < activeIndex)
                {
                    // Abgeschlossener Schritt: Gedämpfte Akzentfarbe (oder voll, je nach Wunsch)
                    _stepIndicators[i].Background = _accentBrush;
                    _stepNumbers[i].Foreground = _textActiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush; // Text ausgrauen
                }
                else
                {
                    // Zukünftiger Schritt: Neutral
                    _stepIndicators[i].Background = _neutralBrush;
                    _stepNumbers[i].Foreground = _textInactiveBrush;
                    _stepTexts[i].Foreground = _textInactiveBrush;
                }
            }

            // Pfeile aktualisieren
            for (int i = 0; i < _stepArrows.Length; i++)
            {
                // Wenn der Schritt *vor* dem Pfeil aktiv/fertig ist, färbe den Pfeil ein
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

        /// <summary>
        /// Zeigt/Verbirgt die "Weiter" / "Fertig" Buttons.
        /// </summary>
        private void UpdateNavButtons(int index)
        {
            // "Zurück"-Button aktivieren/deaktivieren
            BackButton.IsEnabled = (index > 0);

            if (index == OnboardingFlipView.Items.Count - 1)
            {
                // Letzte Seite
                NextButton.Visibility = Visibility.Collapsed;
                FinishButton.Visibility = Visibility.Visible;
            }
            else
            {
                // Jede andere Seite
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
            // Diese Aktion schließt das Onboarding ab.
            // Du musst dem übergeordneten Fenster mitteilen, dass es diese Seite schließen soll.
            Debug.WriteLine("Onboarding Finished!");

            // Finde das übergeordnete 'Frame' und navigiere zurück (oder schließe das Dialogfenster)
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is Frame frame && frame.CanGoBack)
                {
                    frame.GoBack();
                    return;
                }
                // Falls es in einem ContentDialog ist:
                if (parent is ContentDialog dialog)
                {
                    dialog.Hide();
                    return;
                }
                parent = (parent as FrameworkElement)?.Parent;
            }
        }
    }
}