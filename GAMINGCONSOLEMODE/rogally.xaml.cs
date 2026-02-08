using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class rogally : Page
    {
        // Flag to prevent saving while we are still loading the initial values
        private bool _isLoaded = false;

        public rogally()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadSavedSettings();
            _isLoaded = true; // Loading finished, now we can react to changes
        }

        private void LoadSavedSettings()
        {
            try
            {
                // Try to load keys from TOML. If they don't exist, Load<T> throws an exception
                string m1Action = "None";
                string m2Action = "None";

                try { m1Action = AppSettings.Load<string>("rog_m1_action"); } catch { }
                try { m2Action = AppSettings.Load<string>("rog_m2_action"); } catch { }

                SetComboSelection(ComboM1, m1Action);
                SetComboSelection(ComboM2, m2Action);

                Debug.WriteLine($"[GCM] RogAlly UI initialized with M1:{m1Action}, M2:{m2Action}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GCM] Error loading RogAlly settings: {ex.Message}");
            }
        }

        private void SetComboSelection(ComboBox combo, string action)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content.ToString() == action)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            combo.SelectedIndex = 0; // Fallback to "None"
        }

        private void ComboM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only save if the user actually changed something (not during initial load)
            if (!_isLoaded) return;

            if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem selectedItem)
            {
                string action = selectedItem.Content.ToString();

                if (combo.Name == "ComboM1")
                {
                    AppSettings.Save("rog_m1_action", action);
                }
                else if (combo.Name == "ComboM2")
                {
                    AppSettings.Save("rog_m2_action", action);
                }

                Debug.WriteLine($"[GCM] Saved {combo.Name} action: {action}");
            }
        }
    }
}