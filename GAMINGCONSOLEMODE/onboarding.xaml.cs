// In your Onboarding.xaml.cs
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class Onboarding : Page
    {
        public Onboarding()
        {
            this.InitializeComponent();
            this.Loaded += Onboarding_Loaded;
        }

        private async void Onboarding_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebView2Async();
        }

        // Final attempt to initialize WebView2 with custom user data folder
        private async System.Threading.Tasks.Task InitializeWebView2Async()
        {
            if (MyWebView == null)
            {
                Debug.WriteLine(" WebView2 control 'MyWebView' not found in XAML.");
                await ShowWebViewErrorDialog("Error", "WebView2 control ('MyWebView') not found in XAML.");
                LoadingSpinner.IsActive = false; LoadingSpinner.Visibility = Visibility.Collapsed;
                return;
            }

            LoadingSpinner.IsActive = true;
            LoadingSpinner.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                // 1. Pfad für Benutzerdaten definieren
                string appDataRoamingPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string webViewUserDataFolder = Path.Combine(appDataRoamingPath, "gcmsettings", "WebView2Data");
                Directory.CreateDirectory(webViewUserDataFolder);
                Debug.WriteLine($"WebView2 User Data Folder set to: {webViewUserDataFolder}");

                // --- KORRIGIERTER AUFRUF ---
                // 2. Umgebung mit der korrekten WinRT-Methode 'CreateWithOptionsAsync' erstellen.
                var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, webViewUserDataFolder, null);
                Debug.WriteLine(" CoreWebView2Environment created successfully.");

                // 3. WebView2-Steuerelement mit der erstellten Umgebung initialisieren.
                //    Dies verwendet die neue Überladung, die in WinAppSDK 1.5 eingeführt wurde.
                await MyWebView.EnsureCoreWebView2Async(environment);
                Debug.WriteLine(" EnsureCoreWebView2Async completed.");
                // --- ENDE DES KORRIGIERTEN AUFRUFS ---

                // 4. Navigation (nur wenn Source nicht in XAML gesetzt ist)
                if (MyWebView.Source == null && MyWebView.CoreWebView2 != null)
                {
                    MyWebView.CoreWebView2.Navigate("https://www.gameconsolemode.com/onboarding");
                    Debug.WriteLine(" Navigation initiated.");
                }
                else if (MyWebView.CoreWebView2 == null)
                {
                    Debug.WriteLine(" CoreWebView2 is null after EnsureCoreWebView2Async, cannot navigate.");
                    throw new InvalidOperationException("CoreWebView2 could not be initialized.");
                }
                else
                {
                    Debug.WriteLine(" Source likely set in XAML or navigation already started.");
                }
            }
            catch (ArgumentException argEx)
            {
                Debug.WriteLine($" Invalid argument during environment creation: {argEx.Message}");
                await ShowWebViewErrorDialog("WebView2 Initialization Error", $"Invalid argument provided for WebView2 environment:\n{argEx.Message}");
                ShowErrorState("Error: Invalid configuration for WebView2.");
            }
            catch (FileNotFoundException fileEx)
            {
                Debug.WriteLine($" Runtime likely missing: {fileEx.Message}");
                await ShowWebViewErrorDialog("WebView2 Error", "The required Microsoft Edge WebView2 Runtime might not be installed. Please install it.");
                ShowErrorState("Error: WebView2 Runtime not found.");
            }
            catch (COMException comEx)
            {
                Debug.WriteLine($" COM Exception: {comEx.Message} (HRESULT: {comEx.HResult:X})");
                string message = $"A COM error occurred during initialization:\n{comEx.Message}";
                string state = $"COM Error: {comEx.HResult:X}";

                if (comEx.HResult == unchecked((int)0x80070005)) // E_ACCESSDENIED
                {
                    message = $"Access denied trying to create WebView2 folder or access Runtime.\nTry running as Administrator.\nDetails: {comEx.Message}";
                    state = "Error: Access denied for WebView2.";
                }
                else if (comEx.HResult == unchecked((int)0x80070002)) // ERROR_FILE_NOT_FOUND
                {
                    message = "The WebView2 Runtime is likely missing or corrupted. Please install/reinstall it.";
                    state = "Error: WebView2 Runtime not found/corrupted.";
                }

                await ShowWebViewErrorDialog("WebView2 COM Error", message);
                ShowErrorState(state);
            }
            catch (UnauthorizedAccessException authEx)
            {
                Debug.WriteLine($" Access denied for user data directory: {authEx.Message}");
                await ShowWebViewErrorDialog("WebView2 Initialization Error", $"No write permissions for the WebView2 data directory:\n{authEx.Message}");
                ShowErrorState("Error: No permissions to write in the WebView2 data folder.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($" Failed to initialize WebView2: {ex.GetType().Name} - {ex.Message}");
                await ShowWebViewErrorDialog("WebView2 Initialization Error", $"An unexpected error occurred:\n{ex.Message}");
                ShowErrorState($"Error: {ex.Message}");
            }
        }

        // Helper method to update the UI when an initialization error occurs
        private void ShowErrorState(string errorMessage)
        {
            DispatcherQueue.TryEnqueue(() => {
                LoadingSpinner.IsActive = false;
                LoadingSpinner.Visibility = Visibility.Collapsed;
                ErrorText.Text = errorMessage;
                ErrorText.Visibility = Visibility.Visible;
                if (MyWebView != null) MyWebView.Opacity = 0; // Hide WebView on error
            });
        }

        // Event handler called when the WebView2 control finishes navigating
        private async void MyWebView_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                LoadingSpinner.IsActive = false;
                LoadingSpinner.Visibility = Visibility.Collapsed;

                if (args.IsSuccess)
                {
                    Debug.WriteLine("[WebView2] Navigation successful.");
                    string script = @"var nav = document.querySelector('.nav-container'); if (nav) nav.style.display = 'none';";
                    try { await MyWebView.ExecuteScriptAsync(script); Debug.WriteLine("[WebView2] Script executed."); }
                    catch (Exception scriptEx) { Debug.WriteLine($"[WebView2 Error] Script failed: {scriptEx.Message}"); }
                    ErrorText.Visibility = Visibility.Collapsed;
                    if (FadeInWebView != null) { FadeInWebView.Begin(); } else { MyWebView.Opacity = 1; }
                }
                else
                {
                    Debug.WriteLine($"[WebView2 Error] Navigation failed: {args.WebErrorStatus}");
                    ErrorText.Text = $"Error loading page: {args.WebErrorStatus}";
                    ErrorText.Visibility = Visibility.Visible;
                    if (MyWebView != null) MyWebView.Opacity = 0;
                }
            });
        }

        // Helper method to display error messages in a ContentDialog
        private async System.Threading.Tasks.Task ShowWebViewErrorDialog(string title, string content)
        {
            if (this.XamlRoot == null) { Debug.WriteLine($"[Dialog Error] XamlRoot null."); return; }
            ContentDialog errorDialog = new ContentDialog { Title = title, Content = content, CloseButtonText = "Ok", XamlRoot = this.XamlRoot };
            try { await errorDialog.ShowAsync(); } catch (Exception dialogEx) { Debug.WriteLine($"[Dialog Error] {dialogEx.Message}"); }
        }
    }
}