using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tomlyn;
using Tomlyn.Model;

namespace GAMINGCONSOLEMODE
{
    public class FunctionViewModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public string Description { get; set; } // New Property
        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }
        public string Key1 { get; set; } = "None";
        public string Key2 { get; set; } = "None";
        public double HoldTime { get; set; } = 0.0;
        public List<string> AvailableButtons { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed partial class shortcuts : Page
    {
        private readonly string[] gamepadButtons = { "None", "A", "B", "X", "Y", "DPadUp", "DPadDown", "DPadLeft", "DPadRight", "Start", "Back", "LeftThumb", "RightThumb", "LeftShoulder", "RightShoulder" };
        private List<FunctionViewModel> allFunctions;

        public shortcuts()
        {
            this.InitializeComponent();
            SetupFunctionCards();
        }

        public static double GetOpacity(bool enabled) => enabled ? 1.0 : 0.4;

        private void SetupFunctionCards()
        {
            // Dictionary for names and descriptions
            var functionDetails = new Dictionary<string, string> {
                { "taskmanager", "Switch to the GCM Taskmanager" },
                { "switch tab", "Simulates Alt+Tab to cycle through your open windows and applications quickly." },
                { "audio switch", "Cycles through your available audio output devices (e.g., switching from Speakers to Headset)." },
                { "performance overlay", "Toggles the GCM performance monitoring tool to view FPS, CPU, and GPU metrics in-game." },
                { "xbox bar", "Triggers the native Windows Game Bar for social features, recording, and broadcasting." },
                { "lossless scaling", "Activates the Lossless Scaling tool to improve image quality and frame rates in windowed games." },
                { "xbox keyboard", "Forces the Windows on-screen keyboard to appear for text input using your controller." }
            };

            allFunctions = functionDetails.Select(kvp => new FunctionViewModel
            {
                Name = kvp.Key,
                Description = kvp.Value,
                AvailableButtons = gamepadButtons.ToList()
            }).ToList();

            FunctionsControl.ItemsSource = allFunctions;
            LoadShortcutSettings();
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
                            card.IsEnabled = Convert.ToBoolean(table["enabled"]);
                            card.Key1 = table["key1"]?.ToString() ?? "None";
                            card.Key2 = table["key2"]?.ToString() ?? "None";
                            card.HoldTime = table.ContainsKey("hold_duration") ? Convert.ToDouble(table["hold_duration"]) : 0.0;
                        }
                    }
                }
            }
            catch { }
        }

        private void _saveConfiguration()
        {
            // 1. Pfade definieren
            string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");

            // 2. Bestehende Einstellungen laden (WICHTIG!)
            TomlTable rootTable;
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    // Datei lesen und in ein bearbeitbares Model parsen
                    string content = File.ReadAllText(settingsFilePath);
                    rootTable = Toml.Parse(content).ToModel();
                }
                else
                {
                    // Sollte eigentlich nicht passieren, da MainWindow sie erstellt, aber zur Sicherheit:
                    rootTable = new TomlTable();
                }
            }
            catch
            {
                // Falls die Datei komplett korrupt ist, fangen wir neu an (oder Loggen Fehler)
                rootTable = new TomlTable();
            }

            // 3. Die neue Shortcut-Liste basierend auf der GUI bauen
            var shortcutList = new TomlTableArray();
            foreach (var card in allFunctions)
            {
                shortcutList.Add(new TomlTable
                {
                    ["function"] = card.Name,
                    ["key1"] = card.Key1,
                    ["key2"] = card.Key2,
                    ["hold_duration"] = card.HoldTime,
                    ["enabled"] = card.IsEnabled
                });
            }

            // 4. NUR die 'shortcuts'-Sektion im geladenen Root-Objekt aktualisieren
            // Alle anderen Werte (launcher, pfade etc.) bleiben im rootTable erhalten!
            rootTable["shortcuts"] = shortcutList;

            // 5. Das gesamte Objekt (Alte Werte + Neue Shortcuts) speichern
            try
            {
                Directory.CreateDirectory(settingsFolder);
                // FromModel serialisiert das komplette Objekt wieder in einen String
                File.WriteAllText(settingsFilePath, Toml.FromModel(rootTable));
            }
            catch (Exception ex)
            {
                // Optional: Debug-Ausgabe, falls Speichern fehlschlägt (z.B. Datei gesperrt)
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Shortcuts: {ex.Message}");
            }
        }

        private void Shortcut_Toggled(object sender, RoutedEventArgs e) => _saveConfiguration();
        private void ControlChanged(object sender, object e) => _saveConfiguration();
    }
}