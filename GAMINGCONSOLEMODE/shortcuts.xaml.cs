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
using Microsoft.Win32.TaskScheduler;
using Button = Microsoft.UI.Xaml.Controls.Button;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Text;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class shortcuts : Page
    {
        // Konstanten für Gamepad-Tasten und Funktionen
        private readonly string[] gamepadButtons = {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "Start", "Back", "LeftThumb", "RightThumb",
            "LeftShoulder", "RightShoulder", "A", "B", "X", "Y"
        };

        private readonly string[] gamepadButtonswin = {
            "DPadUp", "DPadDown", "DPadLeft", "LeftThumb", "RightThumb", "DPadRight",
            "Start", "Back", "A", "B", "X", "Y"
        };

        private readonly List<string> functions = new() {
            "taskmanager", "switch tab", "audio switch",
            "performance overlay", "show overlay"
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
        // #region TOML Speichern & Laden
        // =================================================================

        private void _saveConfiguration()
        {
            string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");
            Directory.CreateDirectory(settingsFolder);

            try
            {
                TomlTable settingsModel = File.Exists(settingsFilePath)
                    ? Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel()
                    : new TomlTable();

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

            ComboBoxswitchgcm1.Items.Clear();
            ComboBoxswitchgcm2.Items.Clear();
            foreach (var btn in gamepadButtonswin)
            {
                ComboBoxswitchgcm1.Items.Add(new ComboBoxItem { Content = btn });
                ComboBoxswitchgcm2.Items.Add(new ComboBoxItem { Content = btn });
            }

            if (loadedKey1 != null) ComboBoxswitchgcm1.SelectedItem = ComboBoxswitchgcm1.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == loadedKey1);
            if (loadedKey2 != null) ComboBoxswitchgcm2.SelectedItem = ComboBoxswitchgcm2.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == loadedKey2);

            // =========================================================================
            // HIER IST DIE KORREKTUR: Diese beiden Zeilen weisen die Event-Handler zu.
            // =========================================================================
            ComboBoxswitchgcm1.SelectionChanged += GamepadComboBox_SelectionChanged;
            ComboBoxswitchgcm2.SelectionChanged += GamepadComboBox_SelectionChanged;

            // Ruft die Methode einmal manuell auf, um den initialen Zustand zu setzen.
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
        // #region UI-Logik und Event-Handler
        // =================================================================

        private void AddCustomShortcut(object sender, RoutedEventArgs e) => AddCustomShortcut();

        private void AddCustomShortcut(string key1 = null, string key2 = null, string function = null, bool enabled = false, bool skipFilter = false)
        {
            var border = new Border { Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 5) };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

            var availableFunctions = skipFilter ? functions : functions.Except(GetUsedFunctions());
            var cbKey1 = CreateStyledComboBox(gamepadButtons, key1);
            var cbKey2 = CreateStyledComboBox(gamepadButtons, key2);
            var cbFunc = CreateStyledComboBox(availableFunctions, function);

            cbKey1.IsEnabled = !enabled;
            cbKey2.IsEnabled = !enabled;
            cbFunc.IsEnabled = !enabled;

            var toggle = new ToggleSwitch { Width = 60, VerticalAlignment = VerticalAlignment.Center, IsOn = enabled };
            var removeBtn = new Button { Content = "✕", Background = new SolidColorBrush(Colors.Brown), Foreground = new SolidColorBrush(Colors.White), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };

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
            foreach (var border in ShortcutPanel.Children.OfType<Border>())
            {
                if (border.Child is StackPanel panel && panel.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == false)
                {
                    var cbFunc = panel.Children.OfType<ComboBox>().ElementAt(2);
                    var currentValue = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    var newItems = functions.Where(f => !usedFunctions.Contains(f) || f == currentValue).ToList();

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

            if (!toggle.IsOn)
            {
                cbKey1.IsEnabled = true; cbKey2.IsEnabled = true; cbFunc.IsEnabled = true;
                UpdateFunctionComboboxes();
                _saveConfiguration();
                return;
            }

            string key1 = (cbKey1.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string key2 = (cbKey2.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrEmpty(key1) || string.IsNullOrEmpty(key2) || string.IsNullOrEmpty(func))
            {
                toggle.IsOn = false; return;
            }
            if (IsCombinationInUse(key1, key2, panel) || IsFunctionInUse(func, panel))
            {
                toggle.IsOn = false; return;
            }

            cbKey1.IsEnabled = false; cbKey2.IsEnabled = false; cbFunc.IsEnabled = false;
            UpdateFunctionComboboxes();
            _saveConfiguration();
        }

        // =================================================================
        // #region Hilfsmethoden
        // =================================================================

        private ComboBox CreateStyledComboBox(IEnumerable<string> items, string selected = null)
        {
            var combo = new ComboBox { Width = 180, PlaceholderText = "..." };
            var finalItems = items.ToList();
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
            bool duplicate = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null && p != currentPanel && p.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == true)
                .Any(p => {
                    string otherKey1 = (p.Children.OfType<ComboBox>().ElementAt(0).SelectedItem as ComboBoxItem)?.Content?.ToString();
                    string otherKey2 = (p.Children.OfType<ComboBox>().ElementAt(1).SelectedItem as ComboBoxItem)?.Content?.ToString();
                    return (key1 == otherKey1 && key2 == otherKey2) || (key1 == otherKey2 && key2 == otherKey1);
                });
            if (duplicate) ShowSimpleDialog("Duplicate Shortcut", $"The combination {key1} + {key2} is already in use.");
            return duplicate;
        }

        private bool IsFunctionInUse(string func, StackPanel currentPanel)
        {
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
                ComboBoxswitchgcm1.IsEnabled = true;
                ComboBoxswitchgcm2.IsEnabled = true;
                AppSettings.Save("useseamlessswitchtogcm", false);
                _saveConfiguration();
                return;
            }

            string key1 = (ComboBoxswitchgcm1.SelectedItem as ComboBoxItem)?.Content?.ToString();
            string key2 = (ComboBoxswitchgcm2.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(key1) || string.IsNullOrWhiteSpace(key2) || key1 == key2 || IsCombinationInUse(key1, key2, null))
            {
                if (key1 == key2) ShowSimpleDialog("Invalid Shortcut", "Key1 and Key2 cannot be the same.");
                toggle.IsOn = false;
                return;
            }

            ComboBoxswitchgcm1.IsEnabled = false;
            ComboBoxswitchgcm2.IsEnabled = false;
            AppSettings.Save("useseamlessswitchtogcm", true);
            ShowSimpleDialog("Enabled", "Seamless Switch will be enabled on the next GCM launch.");
            _saveConfiguration();
        }

        private void shortcutpopup_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle) AppSettings.Save("shortcutpopup", toggle.IsOn);
        }
        #endregion
    }
}
