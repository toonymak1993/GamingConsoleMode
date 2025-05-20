using Microsoft.UI.Xaml;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GAMINGCONSOLEMODE
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static MainWindow MainWindow { get; private set; }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        /// 
      

        public App()
        {
            this.InitializeComponent();

        }
        public static Window? Window { get; private set; }
        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                MainWindow = new MainWindow();
                m_window = MainWindow;
                m_window.Activate();

            }
            catch (Exception ex)
            {
                // Falls ein Fehler auftritt, zeige eine MessageBox mit der Fehlermeldung
                ShowErrorMessage(ex);
            }
        }

        // Methode zur Anzeige einer MessageBox
        private void ShowErrorMessage(Exception ex)
        {
            // Erstelle eine neue MessageBox mit dem Fehlertext
            var messageDialog = new Windows.UI.Popups.MessageDialog($"An error occurred: {ex.Message}\n\n{ex.StackTrace}", "Error");
            _ = messageDialog.ShowAsync();
        }


        private Window? m_window;
    }
}
