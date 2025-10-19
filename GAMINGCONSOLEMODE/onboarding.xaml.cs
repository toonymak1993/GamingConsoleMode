using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml; // Needed for Visibility
using System; // Needed for async/await

namespace GAMINGCONSOLEMODE
{
    public sealed partial class Onboarding : Page
    {
        public Onboarding()
        {
            this.InitializeComponent();
        }

        // We make this 'async' to 'await' the script execution
        private async void MyWebView_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // This event often runs on a background thread.
            // We must update the UI (spinner, opacity) back on the main UI thread.
            DispatcherQueue.TryEnqueue(async () =>
            {
                // Stop the spinner, no matter what happened
                LoadingSpinner.IsActive = false;
                LoadingSpinner.Visibility = Visibility.Collapsed;

                if (args.IsSuccess)
                {
                    // --- THIS IS THE NEW PART ---
                    // JavaScript to find the nav-container and hide it.
                    string script = "document.querySelector('.nav-container').style.display = 'none';";

                    // Run the script inside the web page
                    await MyWebView.ExecuteScriptAsync(script);
                    // --------------------------

                    // All good. Start the fade-in animation.
                    FadeInWebView.Begin();
                }
                else
                {
                    // Failed (e.g., no internet connection)
                    // Show the error message instead.
                    ErrorText.Visibility = Visibility.Visible;
                }
            });
        }
    }
}