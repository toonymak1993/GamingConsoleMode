using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using ClickableTransparentOverlay;
using ClickableTransparentOverlay.Win32;
using ImGuiNET;

namespace gcmloader
{
    internal class OverlayWindow : Overlay
    {
        private readonly string shortcutPath;
        private List<Shortcut> activeShortcuts = new();
        private Thread logicThread;
        private volatile bool isRunning = true;

        public OverlayWindow() : base(1920, 1080)
        {
            shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "gcmsettings", "shortcuts");

            // Start logic thread
            logicThread = new Thread(() =>
            {
                while (isRunning)
                {
                    LoadShortcuts();
                    Thread.Sleep(2000);
                }
            });
            logicThread.Start();
        }

        public override void Close()
        {
            base.Close();
            isRunning = false;
        }

        protected override void Render()
        {
            ImGui.SetNextWindowPos(new Vector2(0, 0));
            ImGui.SetNextWindowSize(new Vector2(300, 1080));
            ImGui.Begin("Shortcut Overlay", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoTitleBar);

            if (ImGui.Button("Close Overlay"))
            {
                Close();
            }

            ImGui.Text("Active Shortcuts:");
            ImGui.Separator();

            foreach (var shortcut in activeShortcuts)
            {
                ImGui.Text($"{shortcut.Key1} + {shortcut.Key2}: {shortcut.Function}");
            }

            ImGui.End();
        }

        private void LoadShortcuts()
        {
            try
            {
                activeShortcuts.Clear();
                var files = Directory.GetFiles(shortcutPath, "*.json");

                foreach (var file in files)
                {
                    string content = File.ReadAllText(file);
                    var shortcut = JsonSerializer.Deserialize<Shortcut>(content);
                    if (shortcut != null && shortcut.Enabled)
                    {
                        activeShortcuts.Add(shortcut);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading shortcuts: " + ex.Message);
            }
        }
    }

    public class Shortcut
    {
        public string Key1 { get; set; }
        public string Key2 { get; set; }
        public string Function { get; set; }
        public bool Enabled { get; set; }
    }
}
