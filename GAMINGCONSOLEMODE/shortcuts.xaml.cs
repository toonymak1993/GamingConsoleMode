using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Diagnostics;
using Tomlyn;
using Tomlyn.Model;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class shortcuts : Page
    {
        // Constants for gamepad buttons and available functions.
        private readonly string[] gamepadButtons = {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "Start", "Back", "LeftThumb", "RightThumb",
            "LeftShoulder", "RightShoulder", "A", "B", "X", "Y"
        };

        // A slightly different list for the "switch to GCM" shortcut.
        private readonly string[] gamepadButtonswin = {
            "DPadUp", "DPadDown", "DPadLeft", "LeftThumb", "RightThumb", "DPadRight",
            "Start", "Back", "A", "B", "X", "Y"
        };

        // The list of functions that can be assigned to a shortcut.
        private readonly List<string> functions = new() {
            "taskmanager", "switch tab", "audio switch",
            "performance overlay", "show overlay", "xbox bar", "lossless scaling", "xbox keyboard"
        };

        public shortcuts()
        {
            this.InitializeComponent();
            this.Loaded += Shortcuts_Loaded;
        }

        private void Shortcuts_Loaded(object sender, RoutedEventArgs e)
        {
            LoadExistingShortcuts();
            insertgamepaddata();
            updateui();
        }

        // =================================================================
        // #region TOML Save & Load
        // =================================================================

        private void _saveConfiguration()
        {
            string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");
            Directory.CreateDirectory(settingsFolder);

            try
            {
                // If the settings file exists, parse it. Otherwise, create a new empty model.
                TomlTable settingsModel = File.Exists(settingsFilePath)
                    ? Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel()
                    : new TomlTable();

                // Create a new list for the shortcuts.
                var shortcutList = new TomlTableArray();
                foreach (var border in ShortcutPanel.Children.OfType<Border>())
                {
                    if (border.Child is StackPanel panel)
                    {
                        var cbKey1 = (ComboBox)panel.Children[0];
                        var cbKey2 = (ComboBox)panel.Children[2];
                        var cbFunc = (ComboBox)panel.Children[4];
                        var toggle = (ToggleSwitch)panel.Children[5];

                        string key1 = (cbKey1.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        string key2 = (cbKey2.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();

                        // Only save if all parts of the shortcut are defined.
                        if (!string.IsNullOrEmpty(key1) && !string.IsNullOrEmpty(key2) && !string.IsNullOrEmpty(func))
                        {
                            shortcutList.Add(new TomlTable
                            {
                                ["key1"] = key1,
                                ["key2"] = key2,
                                ["function"] = func,
                                ["enabled"] = toggle.IsOn
                            });
                        }
                    }
                }
                settingsModel["shortcuts"] = shortcutList;

                // Handle the special "switch to GCM" shortcut.
                string winKey1 = (ComboBoxswitchgcm1.SelectedItem as ComboBoxItem)?.Content?.ToString();
                string winKey2 = (ComboBoxswitchgcm2.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (winswitchgcm.IsOn && !string.IsNullOrEmpty(winKey1) && !string.IsNullOrEmpty(winKey2))
                {
                    settingsModel["winmode_shortcut"] = new TomlTable
                    {
                        ["key1"] = winKey1,
                        ["key2"] = winKey2,
                        ["enabled"] = true
                    };
                }
                else
                {
                    // If it's disabled, remove it from the config.
                    settingsModel.Remove("winmode_shortcut");
                }

                File.WriteAllText(settingsFilePath, Toml.FromModel(settingsModel));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving to TOML: {ex.Message}");
            }
        }

        private void LoadExistingShortcuts()
        {
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");
            if (!File.Exists(settingsFilePath)) return;

            try
            {
                var model = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();
                if (model.TryGetValue("shortcuts", out var shortcutsObj) && shortcutsObj is TomlTableArray shortcutsArray)
                {
                    // Recreate the UI for each shortcut found in the file.
                    foreach (TomlTable table in shortcutsArray)
                    {
                        AddCustomShortcut(table["key1"]?.ToString(), table["key2"]?.ToString(), table["function"]?.ToString(), Convert.ToBoolean(table["enabled"]), true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading shortcuts from TOML: {ex.Message}");
            }
            // After loading, make sure the function dropdowns are updated.
            DispatcherQueue.TryEnqueue(UpdateFunctionComboboxes);
        }

        private void insertgamepaddata()
        {
            string settingsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");
            string loadedKey1 = null, loadedKey2 = null;
            bool enabled = false;

            if (File.Exists(settingsFilePath))
            {
                try
                {
                    var model = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();
                    if (model.TryGetValue("winmode_shortcut", out var shortcutObj) && shortcutObj is TomlTable table)
                    {
                        loadedKey1 = table["key1"]?.ToString();
                        loadedKey2 = table["key2"]?.ToString();
                        enabled = Convert.ToBoolean(table["enabled"]);
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Error loading winmode_shortcut: {ex.Message}"); }
            }

            // Populate the dropdowns with available buttons.
            ComboBoxswitchgcm1.Items.Clear();
            ComboBoxswitchgcm2.Items.Clear();
            foreach (var btn in gamepadButtonswin)
            {
                ComboBoxswitchgcm1.Items.Add(new ComboBoxItem { Content = btn });
                ComboBoxswitchgcm2.Items.Add(new ComboBoxItem { Content = btn });
            }

            // Set the selected items based on what was loaded from the file.
            if (loadedKey1 != null) ComboBoxswitchgcm1.SelectedItem = ComboBoxswitchgcm1.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == loadedKey1);
            if (loadedKey2 != null) ComboBoxswitchgcm2.SelectedItem = ComboBoxswitchgcm2.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == loadedKey2);

            // This is where we assign the event handlers.
            ComboBoxswitchgcm1.SelectionChanged += GamepadComboBox_SelectionChanged;
            ComboBoxswitchgcm2.SelectionChanged += GamepadComboBox_SelectionChanged;

            // Manually call the method once to set the initial state of the toggle.
            GamepadComboBox_SelectionChanged(null, null);

            if (enabled && ComboBoxswitchgcm1.SelectedItem != null && ComboBoxswitchgcm2.SelectedItem != null)
            {
                winswitchgcm.IsOn = true;
                ComboBoxswitchgcm1.IsEnabled = false;
                ComboBoxswitchgcm2.IsEnabled = false;
            }
            else
            {
                winswitchgcm.IsOn = false;
            }
        }

        // =================================================================
        // #region UI Logic and Event Handlers
        // =================================================================

        private void AddCustomShortcut(object sender, RoutedEventArgs e) => AddCustomShortcut();

        private void AddCustomShortcut(string key1 = null, string key2 = null, string function = null, bool enabled = false, bool skipFilter = false)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 5) };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

            // When loading, `skipFilter` is true so all functions are available. When adding a new row, we filter out used ones.
            var availableFunctions = skipFilter ? functions : functions.Except(GetUsedFunctions());
            var cbKey1 = CreateStyledComboBox(gamepadButtons, key1);
            var cbKey2 = CreateStyledComboBox(gamepadButtons, key2);
            var cbFunc = CreateStyledComboBox(availableFunctions, function);

            cbKey1.IsEnabled = !enabled;
            cbKey2.IsEnabled = !enabled;
            cbFunc.IsEnabled = !enabled;

            var toggle = new ToggleSwitch { Width = 60, VerticalAlignment = VerticalAlignment.Center, IsOn = enabled };
            var removeBtn = new Button { Content = "✕", Background = new SolidColorBrush(Colors.Brown), Foreground = new SolidColorBrush(Colors.White), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };

            // Wire up events to save configuration when changes are made.
            cbKey1.SelectionChanged += (s, e) => _saveConfiguration();
            cbKey2.SelectionChanged += (s, e) => _saveConfiguration();
            cbFunc.SelectionChanged += (s, e) => _saveConfiguration();
            toggle.Toggled += ToggleSwitch_Toggled;
            removeBtn.Click += (s, args) =>
            {
                ShortcutPanel.Children.Remove(border);
                UpdateFunctionComboboxes();
                _saveConfiguration();
            };

            // Assemble the UI row.
            panel.Children.Add(cbKey1);
            panel.Children.Add(new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center, FontSize = 20 });
            panel.Children.Add(cbKey2);
            panel.Children.Add(new TextBlock { Text = "=", VerticalAlignment = VerticalAlignment.Center, FontSize = 20 });
            panel.Children.Add(cbFunc);
            panel.Children.Add(toggle);
            panel.Children.Add(removeBtn);
            border.Child = panel;
            ShortcutPanel.Children.Add(border);
        }

        private void UpdateFunctionComboboxes()
        {
            var usedFunctions = GetUsedFunctions();
            // Go through each shortcut row.
            foreach (var border in ShortcutPanel.Children.OfType<Border>())
            {
                // Only update dropdowns for shortcuts that are currently disabled.
                if (border.Child is StackPanel panel && panel.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == false)
                {
                    var cbFunc = panel.Children.OfType<ComboBox>().ElementAt(2);
                    var currentValue = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    // The new list of items should contain all unused functions, plus the one currently selected in this row.
                    var newItems = functions.Where(f => !usedFunctions.Contains(f) || f == currentValue).ToList();

                    // Update the UI on the main thread.
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        cbFunc.Items.Clear();
                        newItems.ForEach(item => cbFunc.Items.Add(new ComboBoxItem { Content = item }));
                        if (currentValue != null)
                        {
                            cbFunc.SelectedItem = cbFunc.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == currentValue);
                        }
                    });
                }
            }
        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (!(toggle?.Parent is StackPanel panel)) return;

            var cbKey1 = (ComboBox)panel.Children[0];
            var cbKey2 = (ComboBox)panel.Children[2];
            var cbFunc = (ComboBox)panel.Children[4];

            // If the shortcut is being disabled...
            if (!toggle.IsOn)
            {
                // ...re-enable the dropdowns and update all lists.
                cbKey1.IsEnabled = true; cbKey2.IsEnabled = true; cbFunc.IsEnabled = true;
                UpdateFunctionComboboxes();
                _saveConfiguration();
                return;
            }

            // If the shortcut is being enabled, we need to validate it first.
            string key1 = (cbKey1.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string key2 = (cbKey2.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(key1) || string.IsNullOrEmpty(key2) || string.IsNullOrEmpty(func))
            {
                toggle.IsOn = false; return; // Can't enable an incomplete shortcut.
            }
            if (IsCombinationInUse(key1, key2, panel) || IsFunctionInUse(func, panel))
            {
                toggle.IsOn = false; return; // Combination or function is a duplicate.
            }

            // If validation passes, lock the dropdowns.
            cbKey1.IsEnabled = false; cbKey2.IsEnabled = false; cbFunc.IsEnabled = false;
            UpdateFunctionComboboxes();
            _saveConfiguration();
        }

        // =================================================================
        // #region Helper Methods
        // =================================================================

        private ComboBox CreateStyledComboBox(IEnumerable<string> items, string selected = null)
        {
            var combo = new ComboBox { Width = 180, PlaceholderText = "..." };
            var finalItems = items.ToList();
            // If a selected item is passed that isn't in the list (e.g., a used function), add it.
            if (selected != null && !finalItems.Contains(selected)) finalItems.Add(selected);

            finalItems.ForEach(item => combo.Items.Add(new ComboBoxItem { Content = item }));
            if (selected != null) combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == selected);

            return combo;
        }

        private void ShowSimpleDialog(string title, string content)
        {
            var dialog = new ContentDialog { Title = title, Content = content, CloseButtonText = "OK", XamlRoot = this.XamlRoot };
            _ = dialog.ShowAsync();
        }

        private List<string> GetUsedFunctions() => ShortcutPanel.Children.OfType<Border>()
            .Select(b => b.Child as StackPanel)
            .Where(p => p?.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == true)
            .Select(p => (p.Children.OfType<ComboBox>().ElementAt(2).SelectedItem as ComboBoxItem)?.Content?.ToString())
            .Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();

        private bool IsCombinationInUse(string key1, string key2, StackPanel currentPanel)
        {
            // Check all other enabled shortcuts for the same key combination.
            bool duplicate = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null && p != currentPanel && p.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == true)
                .Any(p => {
                    string otherKey1 = (p.Children.OfType<ComboBox>().ElementAt(0).SelectedItem as ComboBoxItem)?.Content?.ToString();
                    string otherKey2 = (p.Children.OfType<ComboBox>().ElementAt(1).SelectedItem as ComboBoxItem)?.Content?.ToString();
                    // Order doesn't matter, so check both ways (e.g., A+B is the same as B+A).
                    return (key1 == otherKey1 && key2 == otherKey2) || (key1 == otherKey2 && key2 == otherKey1);
                });
            if (duplicate) ShowSimpleDialog("Duplicate Shortcut", $"The combination {key1} + {key2} is already in use.");
            return duplicate;
        }

        private bool IsFunctionInUse(string func, StackPanel currentPanel)
        {
            // Check all other enabled shortcuts for the same function.
            bool duplicate = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Any(p => p != null && p != currentPanel && p.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == true && (p.Children.OfType<ComboBox>().ElementAt(2).SelectedItem as ComboBoxItem)?.Content?.ToString() == func);
            if (duplicate) ShowSimpleDialog("Function Already Used", $"The function \"{func}\" is already assigned.");
            return duplicate;
        }

        #region Other UI Handlers
        private void updateui()
        {
            try { shortcutpopup.IsOn = AppSettings.Load<bool>("shortcutpopup"); }
            catch { AppSettings.Save("shortcutpopup", true); shortcutpopup.IsOn = true; }
        }

        private void SettingsToggle_Click(object sender, RoutedEventArgs e) => SettingsContent.Visibility = SettingsToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        private void GamepadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // The main toggle switch should only be enabled if both keys are selected.
            winswitchgcm.IsEnabled = ComboBoxswitchgcm1.SelectedItem != null && ComboBoxswitchgcm2.SelectedItem != null;
            if (winswitchgcm.IsOn)
            {
                _saveConfiguration();
            }
        }

        private void winswitchgcm_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (!toggle.IsOn)
            {
                // If disabled, unlock the dropdowns.
                ComboBoxswitchgcm1.IsEnabled = true;
                ComboBoxswitchgcm2.IsEnabled = true;
                AppSettings.Save("useseamlessswitchtogcm", false);
                _saveConfiguration();
                return;
            }

            string key1 = (ComboBoxswitchgcm1.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string key2 = (ComboBoxswitchgcm2.SelectedItem as ComboBoxItem)?.Content?.ToString();

            // Validate before enabling.
            if (string.IsNullOrWhiteSpace(key1) || string.IsNullOrWhiteSpace(key2) || key1 == key2 || IsCombinationInUse(key1, key2, null))
            {
                if (key1 == key2) ShowSimpleDialog("Invalid Shortcut", "Key1 and Key2 cannot be the same.");
                toggle.IsOn = false; // Revert the toggle if validation fails.
                return;
            }

            // If valid, lock the dropdowns and save.
            ComboBoxswitchgcm1.IsEnabled = false;
            ComboBoxswitchgcm2.IsEnabled = false;
            AppSettings.Save("useseamlessswitchtogcm", true);

            _saveConfiguration();
        }

        private void shortcutpopup_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) AppSettings.Save("shortcutpopup", toggle.IsOn);
        }
        #endregion
    }
}
