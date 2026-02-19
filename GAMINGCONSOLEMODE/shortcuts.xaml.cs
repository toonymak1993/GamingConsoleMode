using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    // Hilfsklasse für die Buttons
    public class ButtonOption
    {
        public string Value { get; set; }           // "A", "B", etc.
        public string XboxIcon { get; set; }        // Pfad zum Bild
        public string PlayStationIcon { get; set; } // Pfad zum Bild

        // Hilfseigenschaft: Zeige Icons nur, wenn es nicht "None" ist
        public bool ShowIcons => Value != "None";
        public bool ShowText => Value == "None";
    }

    // Converter: Macht UI-Elemente sichtbar oder unsichtbar
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            (bool)value ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotImplementedException();
    }

    public class FunctionViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Description { get; set; }

        private bool _isEnabled;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        // Gespeicherte Werte (z.B. "A")
        private string _key1 = "None";
        public string Key1
        {
            get => _key1;
            set { _key1 = value; OnPropertyChanged(nameof(Key1)); }
        }

        private string _key2 = "None";
        public string Key2
        {
            get => _key2;
            set { _key2 = value; OnPropertyChanged(nameof(Key2)); }
        }

        public double HoldTime { get; set; } = 0.0;

        // Liste der Optionen (Bilder + Text)
        public List<ButtonOption> AvailableButtons { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class shortcuts : Page
    {
        // Die Namen müssen EXAKT so heißen wie deine Bilddateien (ohne .png)
        private readonly string[] gamepadButtons = {
    "None", "Guide", "A", "B", "X", "Y",
    "DPadUp", "DPadDown", "DPadLeft", "DPadRight",
    "Start", "Back", "LeftThumb", "RightThumb",
    "LeftShoulder", "RightShoulder"
};
        private List<FunctionViewModel> allFunctions;
        private bool _isLoading = true;

        public shortcuts()
        {
            this.InitializeComponent();
            SetupFunctionCards();
            _isLoading = false;
        }

        private void SetupFunctionCards()
        {
            var functionDetails = new Dictionary<string, string> {
                { "taskmanager", "Switch to the GCM Taskmanager" },
                { "shortcut overlay", "Displays all your active shortcuts, be careful with full-screen games." },
                { "kill process", "Kill current process or window/game" },//new
                { "switch tab", "Simulates Alt+Tab to cycle through your open windows and applications quickly." },
                { "audio switch", "Cycles through your available audio output devices (e.g., switching from Speakers to Headset)." },
                { "volume up", "Kill current process or window/game" }, //new
                { "volume down", "Kill current process or window/game" },//new
                { "performance overlay", "Toggles the Nvidia Amd performance monitoring tool to view FPS, CPU, and GPU metrics in-game." },
                { "xbox bar", "Triggers the native Windows Game Bar for social features, recording, and broadcasting." },
                { "xbox keyboard", "Forces the Windows on-screen keyboard to appear for text input using your controller." },
                { "lossless scaling", "Activates the Lossless Scaling tool to improve image quality and frame rates in windowed games." }
            };

            // Hier bauen wir die Pfade zu den Bildern
            // Achte darauf, dass deine Ordner genau so heißen: Assets/controllericons/xbox/
            // Hier bauen wir die Pfade zu den Bildern
            var buttonOptions = gamepadButtons.Select(btn =>
            {
                // Dateiname bestimmen: Wenn "Guide", dann nimm "xbox.png", sonst den Button-Namen (z.B. "A.png")
                string fileName = (btn == "Guide") ? "xbox" : btn;

                return new ButtonOption
                {
                    Value = btn,
                    // Nutze Kleinschreibung für die Ordner, falls sie so heißen
                    XboxIcon = $"ms-appx:///Assets/controllericons/xbox/{fileName}.png",
                    PlayStationIcon = $"ms-appx:///Assets/controllericons/playstation/{fileName}.png"
                };
            }).ToList();

            allFunctions = functionDetails.Select(kvp => new FunctionViewModel
            {
                Name = kvp.Key,
                Description = kvp.Value,
                AvailableButtons = buttonOptions
            }).ToList();

            LoadShortcutSettings();
            FunctionsControl.ItemsSource = allFunctions;
        }

        private void LoadShortcutSettings()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "settings.toml");
            if (!File.Exists(path)) return;

            try
            {
                var model = Toml.Parse(File.ReadAllText(path)).ToModel();
                if (model.TryGetValue("shortcuts", out var obj) && obj is TomlTableArray array)
                {
                    foreach (TomlTable table in array)
                    {
                        string funcName = table["function"]?.ToString();
                        var card = allFunctions.FirstOrDefault(f => f.Name == funcName);
                        if (card != null)
                        {
                            if (table.ContainsKey("enabled")) card.IsEnabled = Convert.ToBoolean(table["enabled"]);

                            // Laden der Keys
                            if (table.ContainsKey("key1")) card.Key1 = table["key1"]?.ToString();
                            if (table.ContainsKey("key2")) card.Key2 = table["key2"]?.ToString();

                            if (table.ContainsKey("hold_duration")) card.HoldTime = Convert.ToDouble(table["hold_duration"]);
                        }
                    }
                }
            }
            catch { }
        }

        private void _saveConfiguration()
        {
            if (_isLoading) return;

            string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");

            TomlTable rootTable;
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    rootTable = Toml.Parse(File.ReadAllText(settingsFilePath)).ToModel();
                }
                else
                {
                    rootTable = new TomlTable();
                }
            }
            catch
            {
                rootTable = new TomlTable();
            }

            var shortcutList = new TomlTableArray();
            foreach (var card in allFunctions)
            {
                shortcutList.Add(new TomlTable
                {
                    ["function"] = card.Name,
                    ["key1"] = card.Key1 ?? "None",
                    ["key2"] = card.Key2 ?? "None",
                    ["hold_duration"] = card.HoldTime,
                    ["enabled"] = card.IsEnabled
                });
            }

            rootTable["shortcuts"] = shortcutList;

            try
            {
                Directory.CreateDirectory(settingsFolder);
                File.WriteAllText(settingsFilePath, Toml.FromModel(rootTable));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
            }
        }

        private void Shortcut_Toggled(object sender, RoutedEventArgs e) => _saveConfiguration();
        private void ControlChanged(object sender, object e) => _saveConfiguration();
    }
}