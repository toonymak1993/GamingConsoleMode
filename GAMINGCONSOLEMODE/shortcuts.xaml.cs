// Fix: Avoid setting function index on load unless creating new
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Win32.TaskScheduler;
using System.Windows.Forms;
using Button = Microsoft.UI.Xaml.Controls.Button;
using ComboBox = Microsoft.UI.Xaml.Controls.ComboBox;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace GAMINGCONSOLEMODE
{
    public sealed partial class shortcuts : Page
    {
        private readonly string[] gamepadButtons = new[]
        {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "Start", "Back", "LeftThumb", "RightThumb",
            "LeftShoulder", "RightShoulder", "A", "B", "X", "Y"
        };

        private readonly string[] gamepadButtonswin = new[]
        {
            "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
            "Start", "Back","A", "B", "X", "Y"
        };

        private readonly List<string> functions = new()
        {
            "taskmanager",
            "switch tab",
            "audio switch",
            "performance overlay",
            "show overlay"
        };

        public shortcuts()
        {
            Debug.WriteLine("Constructor: shortcuts() started");

            this.InitializeComponent();
            this.Loaded += Shortcuts_Loaded;

            Debug.WriteLine("Constructor: shortcuts() finished");
        }


        private void Shortcuts_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Page loaded → calling LoadExistingShortcuts()");
            LoadExistingShortcuts();
            insertgamepaddata();

            var uri = new Uri("https://upload.wikimedia.org/wikipedia/commons/thumb/f/f9/Windows365-logo.svg/960px-Windows365-logo.svg.png");
            var bitmap = new BitmapImage(uri);
            WebImage.Source = bitmap;

        }


        private List<string> GetUsedFunctions()
        {
            return ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null)
                .Where(p => p.Children.OfType<ToggleSwitch>().FirstOrDefault()?.IsOn == true)
                .Select(p => p.Children.OfType<ComboBox>().ElementAt(2))
                .Select(cb => (cb.SelectedItem as ComboBoxItem)?.Content?.ToString())
                .Where(f => !string.IsNullOrEmpty(f))
                .Distinct()
                .ToList();
        }

        private void UpdateFunctionComboboxes()
        {
            Debug.WriteLine("UpdateFunctionComboboxes started");

            var usedFunctions = GetUsedFunctions();
            Debug.WriteLine("Used functions: " + string.Join(", ", usedFunctions));

            foreach (var border in ShortcutPanel.Children.OfType<Border>())
            {
                var panel = border.Child as StackPanel;
                if (panel == null)
                {
                    Debug.WriteLine("Panel null – skipping");
                    continue;
                }

                var cbFunc = panel.Children.OfType<ComboBox>().ElementAt(2);
                var toggle = panel.Children.OfType<ToggleSwitch>().FirstOrDefault();

                if (toggle?.IsOn == true)
                {
                    Debug.WriteLine("Toggle is ON – skipping combobox update");
                    continue;
                }

                var currentValue = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();
                Debug.WriteLine("Updating ComboBox. Current Value: " + currentValue);

                var newItems = new List<ComboBoxItem>();

                foreach (var func in functions)
                {
                    if (!usedFunctions.Contains(func) || func == currentValue)
                        newItems.Add(new ComboBoxItem { Content = func });
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    Debug.WriteLine("DispatcherQueue: Updating ComboBox.Items");

                    cbFunc.Items.Clear();
                    foreach (var item in newItems)
                        cbFunc.Items.Add(item);

                    var selected = cbFunc.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content?.ToString() == currentValue);
                    if (selected != null)
                    {
                        cbFunc.SelectedItem = selected;
                        Debug.WriteLine("Re-selected current item: " + currentValue);
                    }
                });
            }

            Debug.WriteLine("UpdateFunctionComboboxes finished");
        }






        private void AddCustomShortcut(object sender, RoutedEventArgs e) => AddCustomShortcut();

        private void AddCustomShortcut(string key1 = null, string key2 = null, string function = null, bool enabled = false, bool skipFunctionFilter = false)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 30, 30)),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 51, 51, 51)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10)
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };

            var cbKey1 = CreateStyledComboBox("KEY1", key1);
            var cbKey2 = CreateStyledComboBox("KEY2", key2);
            var cbFunc = CreateStyledComboBox("Function", function);
            cbFunc.Items.Clear();
            var usedFunctions = skipFunctionFilter ? new List<string>() : GetUsedFunctions();
            var addedSet = new HashSet<string>();

            foreach (var func in functions)
            {
                if ((!usedFunctions.Contains(func) || func == function) && !addedSet.Contains(func))
                {
                    cbFunc.Items.Add(new ComboBoxItem { Content = func });
                    addedSet.Add(func);
                }
            }

            if (!string.IsNullOrEmpty(function))
                cbFunc.SelectedItem = cbFunc.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == function);
            cbFunc.IsEnabled = !enabled;
            cbKey1.IsEnabled = !enabled;
            cbKey2.IsEnabled = !enabled;


            var plus = new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center, FontSize = 20, Width = 20, Foreground = new SolidColorBrush(Colors.White) };
            var equals = new TextBlock { Text = "=", VerticalAlignment = VerticalAlignment.Center, FontSize = 20, Width = 20, Foreground = new SolidColorBrush(Colors.White) };
            var toggle = new ToggleSwitch
            {
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center,
                IsOn = enabled,
                IsEnabled = true // Toggle ist immer aktivierbar
            };
            toggle.Toggled += (s, e) => {
                cbFunc.IsEnabled = !toggle.IsOn;
                ToggleSwitch_Toggled(s, e);
            };

            var removeBtn = new Button
            {
                Content = "DEL",
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 51, 51, 51)),
                Foreground = new SolidColorBrush(Colors.White),
                Padding = new Thickness(5, 5, 5, 5),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            removeBtn.Click += (s, args) => {
                ShortcutPanel.Children.Remove(border);
                DeleteShortcutFile(cbFunc);
            };

            cbKey1.SelectionChanged += (s, e) => {
                if (toggle.IsOn)
                {
                    var k1 = (cbKey1.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    var k2 = (cbKey2.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    var f = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (string.IsNullOrEmpty(k1) || k1 == "KEY1" || string.IsNullOrEmpty(k2) || k2 == "KEY2" || string.IsNullOrEmpty(f) || f == "Function")
                    {
                        toggle.IsOn = false;
                        return;
                    }
                }
                SaveShortcutConfig(cbKey1, cbKey2, cbFunc, toggle.IsOn);
            };
            cbKey2.SelectionChanged += (s, e) => { if (toggle.IsOn) SaveShortcutConfig(cbKey1, cbKey2, cbFunc, toggle.IsOn); };
            cbFunc.SelectionChanged += (s, e) =>
            {
                if (toggle.IsOn)
                {
                    cbFunc.IsEnabled = false;
                    SaveShortcutConfig(cbKey1, cbKey2, cbFunc, toggle.IsOn);
                }
                else
                {
                  
                }
            };


            panel.Children.Add(cbKey1);
            panel.Children.Add(plus);
            panel.Children.Add(cbKey2);
            panel.Children.Add(equals);
            panel.Children.Add(cbFunc);
            panel.Children.Add(toggle);
            panel.Children.Add(removeBtn);

            border.Child = panel;
            ShortcutPanel.Children.Add(border);
        }

        private ComboBox CreateStyledComboBox(string placeholder, string selected = null)
        {
            var combo = new ComboBox
            {
                Width = 200,
                PlaceholderText = placeholder,
                Background = new SolidColorBrush(ColorHelper.FromArgb(255, 40, 40, 40)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 70, 70, 70))
            };
            foreach (var btn in gamepadButtons)
                combo.Items.Add(new ComboBoxItem { Content = btn });
            if (!string.IsNullOrEmpty(selected))
                combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Content.ToString() == selected);
            return combo;
        }

        private void LoadExistingShortcuts()
        {
            Debug.WriteLine("LoadExistingShortcuts started");

            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcuts");
            if (!Directory.Exists(dir))
            {
                Debug.WriteLine("Shortcut directory not found: " + dir);
                return;
            }

            var seenCombos = new HashSet<string>();
            var files = Directory.GetFiles(dir, "*.json");
            Debug.WriteLine($"Found {files.Length} shortcut files");

            foreach (var file in files)
            {
                Debug.WriteLine("Processing file: " + Path.GetFileName(file));
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<ShortcutData>(json);

                    if (data != null)
                    {
                        string comboKey = $"{data.Key1}-{data.Key2}";
                        string comboKeyRev = $"{data.Key2}-{data.Key1}";

                        if (!seenCombos.Contains(comboKey) && !seenCombos.Contains(comboKeyRev))
                        {
                            seenCombos.Add(comboKey);
                            Debug.WriteLine($"Adding shortcut: {data.Key1} + {data.Key2} = {data.Function}");
                            AddCustomShortcut(data.Key1, data.Key2, data.Function, data.Enabled, skipFunctionFilter: true);
                        }
                        else
                        {
                            Debug.WriteLine($"Duplicate combo, deleting file: {file}");
                            File.Delete(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error loading shortcut: " + ex.Message);
                }
            }

            Debug.WriteLine("Calling UpdateFunctionComboboxes from LoadExistingShortcuts");
            DispatcherQueue.TryEnqueue(() =>
            {
                Debug.WriteLine("Dispatcher: Running UpdateFunctionComboboxes");
                UpdateFunctionComboboxes();
            });
        }




        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            var panel = toggle?.Parent as StackPanel;
            if (panel == null) return;
            var cbKey1 = panel.Children.OfType<ComboBox>().ElementAt(0);
            var cbKey2 = panel.Children.OfType<ComboBox>().ElementAt(1);
            var cbFunc = panel.Children.OfType<ComboBox>().ElementAt(2);

            // 🛑 Wenn der Toggle gerade deaktiviert wird → nichts tun
            if (!toggle.IsOn)
            {
                cbKey1.IsEnabled = true;
                cbKey2.IsEnabled = true;
                cbFunc.IsEnabled = true;
                SaveShortcutConfig(cbKey1, cbKey2, cbFunc, toggle.IsOn);
                return;
            }

            

            string key1 = (cbKey1.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KEY1";
            string key2 = (cbKey2.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "KEY2";
            string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Function";

            if (key1 == "KEY1" || key2 == "KEY2" || func == "Function")
            {
                toggle.IsOn = false;

                return;
            }

            // Duplikate: Kombi
            bool duplicateCombo = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null && p != panel)
                .Any(p =>
                {
                    var otherToggle = p.Children.OfType<ToggleSwitch>().FirstOrDefault();
                    var otherCb1 = p.Children.OfType<ComboBox>().ElementAt(0);
                    var otherCb2 = p.Children.OfType<ComboBox>().ElementAt(1);

                    string otherKey1 = (otherCb1.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    string otherKey2 = (otherCb2.SelectedItem as ComboBoxItem)?.Content?.ToString();

                    bool sameCombo = (key1 == otherKey1 && key2 == otherKey2) || (key1 == otherKey2 && key2 == otherKey1);

                    return otherToggle?.IsOn == true && sameCombo;
                });

            if (duplicateCombo)
            {
                // ❗ Wichtig: NICHT SaveShortcutConfig aufrufen!
                toggle.IsOn = false;
                ShowSimpleDialog("Duplicate Shortcut", $"The combination {key1} + {key2} is already in use.");
                return;
            }

            // Duplikate: Funktion
            bool duplicateFunction = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null && p != panel)
                .Any(p =>
                {
                    var otherToggle = p.Children.OfType<ToggleSwitch>().FirstOrDefault();
                    var otherFunc = p.Children.OfType<ComboBox>().ElementAt(2);
                    string otherFuncValue = (otherFunc.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    return otherToggle?.IsOn == true && otherFuncValue == func;
                });

            if (duplicateFunction)
            {
                toggle.IsOn = false;
                ShowSimpleDialog("Function Already Used", $"The function \"{func}\" is already assigned to another shortcut.");
                return;
            }

            // ✅ Nur wenn alles erlaubt ist → speichern
            SaveShortcutConfig(cbKey1, cbKey2, cbFunc, toggle.IsOn);
            UpdateFunctionComboboxes();

            // Sperren der ComboBoxen bei manuellem Toggle
            cbKey1.IsEnabled = !toggle.IsOn;
            cbKey2.IsEnabled = !toggle.IsOn;
            cbFunc.IsEnabled = !toggle.IsOn;

        }




        private void SaveShortcutConfig(ComboBox cb1, ComboBox cb2, ComboBox cbFunc, bool isEnabled)
        {
            string key1 = (cb1.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None";
            string key2 = (cb2.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "None";
            string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Function";

            if (key1 == "KEY1" || key2 == "KEY2" || func == "Function")
                return; // Shortcut not save when not all inside

            var data = new ShortcutData
            {
                Key1 = key1,
                Key2 = key2,
                Function = func,
                Enabled = isEnabled
            };

            string json = JsonSerializer.Serialize(data);
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcuts");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, func + ".json");
            File.WriteAllText(path, json);
        }


        private void DeleteShortcutFile(ComboBox cbFunc)
        {
            string func = (cbFunc.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Function";
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcuts");
            string path = Path.Combine(dir, func + ".json");

            bool otherStillActive = ShortcutPanel.Children.OfType<Border>()
                .Select(b => b.Child as StackPanel)
                .Where(p => p != null)
                .Any(p =>
                {
                    var combo = p.Children.OfType<ComboBox>().ElementAt(2);
                    var toggle = p.Children.OfType<ToggleSwitch>().FirstOrDefault();
                    var item = combo.SelectedItem as ComboBoxItem;
                    return combo != cbFunc && item != null && item.Content?.ToString() == func && toggle?.IsOn == true;
                });

            if (!otherStillActive && File.Exists(path))
                File.Delete(path);
        }

        private class ShortcutData
        {
            public string Key1 { get; set; }
            public string Key2 { get; set; }
            public string Function { get; set; }
            public bool Enabled { get; set; }
        }

        #region winshortcuts

        //helper
        public static bool IsTaskActive(string taskName)
        {
            using (TaskService ts = new TaskService())
            {
                var task = ts.FindTask(taskName, true);
                return task != null && task.Enabled;
            }
        }

        // Populates the ComboBoxes with available gamepad buttons and initializes toggle state
        private void insertgamepaddata()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcutswin");
            string path = Path.Combine(dir, "winmode_change.json");

            string loadedKey1 = "";
            string loadedKey2 = "";
            ComboBoxItem selectedItem1 = null;
            ComboBoxItem selectedItem2 = null;

            // Try to read saved shortcut data if file exists
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<ShortcutData>(json);
                    if (data != null)
                    {
                        loadedKey1 = data.Key1?.Trim();
                        loadedKey2 = data.Key2?.Trim();
                    }
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Failed to load winmode_change.json");
                }
            }

            // Populate gamepad button ComboBoxes
            foreach (var btn in gamepadButtonswin)
            {
                var item1 = new ComboBoxItem { Content = btn };
                ComboBoxswitchgcm1.Items.Add(item1);
                if (btn == loadedKey1) selectedItem1 = item1;

                var item2 = new ComboBoxItem { Content = btn };
                ComboBoxswitchgcm2.Items.Add(item2);
                if (btn == loadedKey2) selectedItem2 = item2;
            }

            // Set selected items AFTER items are added
            ComboBoxswitchgcm1.SelectedItem = selectedItem1;
            ComboBoxswitchgcm2.SelectedItem = selectedItem2;

            // Disable toggle switch until valid selections are made
            winswitchgcm.IsEnabled = false;

            // Attach event handlers to enable switch when valid selection is made
            ComboBoxswitchgcm1.SelectionChanged += GamepadComboBox_SelectionChanged;
            ComboBoxswitchgcm2.SelectionChanged += GamepadComboBox_SelectionChanged;

            // Manually trigger once after loading
            GamepadComboBox_SelectionChanged(null, null);

            // Set toggle ON only if valid selection and file exists
            if (selectedItem1 != null && selectedItem2 != null && File.Exists(path))
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
        private void ShowSimpleDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };

            _ = dialog.ShowAsync();
        }
        // Enables the toggle switch only if both ComboBoxes have a valid selection
        private void GamepadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string key1 = (ComboBoxswitchgcm1.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";
            string key2 = (ComboBoxswitchgcm2.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";

            winswitchgcm.IsEnabled = !string.IsNullOrWhiteSpace(key1) && !string.IsNullOrWhiteSpace(key2);
        }

        // Handles toggle switch ON/OFF behavior for saving/removing the gamepad shortcut

        private bool IsCombinationAlreadyInShortcuts(string key1, string key2)
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcuts");

            if (!Directory.Exists(dir))
                return false;

            foreach (var file in Directory.GetFiles(dir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<ShortcutData>(json);

                    if (data == null)
                        continue;

                    System.Diagnostics.Debug.WriteLine($"📝 Checking file {file}: {data.Key1} + {data.Key2} / Enabled: {data.Enabled}");

                    if (data.Enabled)
                    {
                        string fileKey1 = data.Key1?.Trim();
                        string fileKey2 = data.Key2?.Trim();

                        bool sameCombo =
                            (key1 == fileKey1 && key2 == fileKey2) ||
                            (key1 == fileKey2 && key2 == fileKey1);

                        if (sameCombo)
                        {
                            System.Diagnostics.Debug.WriteLine("❌ DUPLICATE COMBINATION DETECTED!");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Error parsing file {file}: {ex.Message}");
                }
            }

            return false;
        }



        private void winswitchgcm_Toggled(object sender, RoutedEventArgs e)
        {
            try
            {
                string key1 = (ComboBoxswitchgcm1.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";
                string key2 = (ComboBoxswitchgcm2.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim() ?? "";
                string func = "winmodechange";

                if (string.IsNullOrWhiteSpace(key1) || string.IsNullOrWhiteSpace(key2))
                {
                    winswitchgcm.IsOn = false;
                    ShowSimpleDialog("Invalid", "Please select two valid keys.");
                    return;
                }

                if (key1 == key2)
                {
                    winswitchgcm.IsOn = false;
                    ShowSimpleDialog("Invalid Shortcut", "Key1 and Key2 cannot be the same.");
                    return;
                }

                // ⛔ Prüfen ob die Kombi bereits als Shortcut gespeichert ist
                if (IsCombinationAlreadyInShortcuts(key1, key2))
                {
                    winswitchgcm.IsOn = false;
                    ShowSimpleDialog("Duplicate Shortcut", $"The combination {key1} + {key2} is already used in a custom shortcut.");
                    return;
                }


                if (sender is ToggleSwitch toggle && toggle.IsOn)
                {


                    // ✅ Shortcut speichern
                    var data = new ShortcutData
                    {
                        Key1 = key1,
                        Key2 = key2,
                        Function = func,
                        Enabled = true 
                    };


                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcutswin");
                    Directory.CreateDirectory(dir);

                    string path = Path.Combine(dir, "winmode_change.json");
                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);

                    System.Diagnostics.Debug.WriteLine($"[ON] Shortcut saved: {key1} + {key2} -> {func} at {path}");

                    // Task aktivieren
                    bool taskExistsAndEnabled = IsTaskActive("GCM_wingamepad");

                    if (!taskExistsAndEnabled)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = @"C:\\Program Files (x86)\\GCMcrew\\GCM\\GCM\\taskHelper\TaskHelper.exe",
                                Arguments = "--enable",
                                UseShellExecute = true,
                                Verb = "runas"
                            };

                            Process.Start(psi);
                            AppSettings.Save("useseamlessswitchtogcm", true);
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            if (ex.NativeErrorCode == 1223)
                            {
                                ShowSimpleDialog("Canceled", "Autostart could not be activated because the process was canceled.");
                                winswitchgcm.IsOn = false;
                                AppSettings.Save("useseamlessswitchtogcm", false);
                                return;
                            }
                            else
                            {
                                ShowSimpleDialog("Error", $"Error when starting the TaskHelper:\n{ex.Message}");
                                return;
                            }
                        }
                    }

                    // Sperre Felder
                    ComboBoxswitchgcm1.IsEnabled = false;
                    ComboBoxswitchgcm2.IsEnabled = false;
                }
                else
                {
                    // OFF: Shortcut löschen
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GCMSettings", "shortcutswin");
                    string path = Path.Combine(dir, "winmode_change.json");

                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        System.Diagnostics.Debug.WriteLine($"[OFF] Shortcut file deleted: {path}");
                    }

                    // Task deaktivieren
                    bool taskExistsAndEnabled = IsTaskActive("GCM_wingamepad");

                    if (taskExistsAndEnabled)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = @"C:\\Program Files (x86)\\GCMcrew\\GCM\\GCM\\taskHelper\TaskHelper.exe",
                                Arguments = "--disable",
                                UseShellExecute = true,
                                Verb = "runas"
                            };

                            Process.Start(psi);
                            AppSettings.Save("useseamlessswitchtogcm", false);
                        }
                        catch (System.ComponentModel.Win32Exception ex)
                        {
                            if (ex.NativeErrorCode == 1223)
                            {
                                ShowSimpleDialog("Canceled", "Autostart could not be deactivated because the process was canceled.");
                                winswitchgcm.IsOn = true;
                                AppSettings.Save("useseamlessswitchtogcm", true);
                                return;
                            }
                            else
                            {
                                ShowSimpleDialog("Error", $"Error when stopping the TaskHelper:\n{ex.Message}");
                                return;
                            }
                        }
                    }

                    // Felder wieder freigeben
                    ComboBoxswitchgcm1.IsEnabled = true;
                    ComboBoxswitchgcm2.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in winswitchgcm_Toggled: {ex.Message}");
            }
        }


        #endregion winshortcuts


    }
}
