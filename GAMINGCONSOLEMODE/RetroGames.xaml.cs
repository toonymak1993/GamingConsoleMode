using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class RetroGames : Page
    {
        private const string HomeUrl = "https://classicgamezone.com";

        public RetroGames()
        {
            this.InitializeComponent();
        }

        private void RetroWebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            if (args.Exception != null) return;
            sender.CoreWebView2.Settings.IsStatusBarEnabled = false;
            sender.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            sender.CoreWebView2.Settings.IsZoomControlEnabled = true;
        }

        private void RetroWebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            AddressBar.Text = args.Uri;
        }

        private void RetroWebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            if (sender.CoreWebView2 != null)
                AddressBar.Text = sender.CoreWebView2.Source;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (RetroWebView.CanGoBack) RetroWebView.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (RetroWebView.CanGoForward) RetroWebView.GoForward();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            RetroWebView.Source = new System.Uri(HomeUrl);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RetroWebView.Reload();
        }
    }
}
