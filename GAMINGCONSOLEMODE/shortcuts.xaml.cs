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
        public string Description { get; set; }
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

        // WICHTIG: Verhindert das Speichern während des Ladens
        private bool _isLoading = true;

        public shortcuts()
        {
            this.InitializeComponent();
            SetupFunctionCards();

            // Erst wenn alles fertig geladen ist, darf gespeichert werden
            _isLoading = false;
        }

        public static double GetOpacity(bool enabled) => enabled ? 1.0 : 0.4;

        private void SetupFunctionCards()
        {
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

            // 1. Zuerst Einstellungen aus der Datei laden (falls vorhanden)
            LoadShortcutSettings();

            // 2. ERST DANACH an die UI binden
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
                            // Sicherheits-Check: Nur setzen wenn Key vorhanden
                            if (table.ContainsKey("enabled")) card.IsEnabled = Convert.ToBoolean(table["enabled"]);
                            card.Key1 = table.ContainsKey("key1") ? table["key1"]?.ToString() : "None";
                            card.Key2 = table.ContainsKey("key2") ? table["key2"]?.ToString() : "None";
                            if (table.ContainsKey("hold_duration")) card.HoldTime = Convert.ToDouble(table["hold_duration"]);
                        }
                    }
                }
            }
            catch { /* Datei korrupt oder altes Format? Einfach ignorieren, wird neu erstellt */ }
        }

        private void _saveConfiguration()
        {
            // Wenn wir noch laden, brechen wir sofort ab!
            if (_isLoading) return;

            string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings");
            string settingsFilePath = Path.Combine(settingsFolder, "settings.toml");

            TomlTable rootTable;
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string content = File.ReadAllText(settingsFilePath);
                    rootTable = Toml.Parse(content).ToModel();
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

        // Diese Events feuern beim Laden der UI automatisch
        private void Shortcut_Toggled(object sender, RoutedEventArgs e) => _saveConfiguration();
        private void ControlChanged(object sender, object e) => _saveConfiguration();
    }
}