using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using WindowsInput;
using WindowsInput.Native;

namespace gcmloader 
{
    /// <summary>
    /// Dynamically reads the hotkey from Lossless Scaling and simulates it
    /// using the reliable WindowsInput library.
    /// Starts the application minimized if it is not already running.
    /// </summary>
    public static class LosslessScalingController
    {
        private const string ProcessName = "LosslessScaling";
        private const string DefaultInstallPath = @"C:\Program Files (x86)\Steam\steamapps\common\Lossless Scaling\LosslessScaling.exe";
        private static readonly InputSimulator inputSimulator = new InputSimulator();

        public static void TriggerScaling()
        {
            (ShortcutInfo shortcut, string errorMessage) = GetScalingShortcut();
            if (errorMessage != null)
            {
                MessageBox.Show(errorMessage, "Error Reading Configuration", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Console.WriteLine($"Hotkey successfully read: {shortcut.Modifiers} + {shortcut.Key}");

            Process lsProcess = GetOrStartLosslessScalingProcess();
            if (lsProcess == null)
            {
                MessageBox.Show("Lossless Scaling process could not be started or found.", "Process Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Console.WriteLine("You now have 3 seconds to click into the target window!");
            Thread.Sleep(3000);

            try
            {
                Console.WriteLine("Sending hotkey with WindowsInput...");
                ExecuteHotkey(shortcut);
                Console.WriteLine("Hotkey has been sent.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during keyboard simulation:\n{ex.Message}", "Simulation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExecuteHotkey(ShortcutInfo shortcut)
        {
            // 1. Convert our modifier flags into a list of VirtualKeyCodes.
            var modifierKeyCodes = new List<VirtualKeyCode>();
            if (shortcut.Modifiers.HasFlag(Modifiers.Control)) modifierKeyCodes.Add(VirtualKeyCode.CONTROL);
            if (shortcut.Modifiers.HasFlag(Modifiers.Alt)) modifierKeyCodes.Add(VirtualKeyCode.MENU); // Alt is VK_MENU
            if (shortcut.Modifiers.HasFlag(Modifiers.Shift)) modifierKeyCodes.Add(VirtualKeyCode.SHIFT);

            // 2. Convert our main key. The enum values are usually identical, so a cast is sufficient.
            var mainKeyCode = (VirtualKeyCode)shortcut.Key;

            // 3. Execute the simulation.
            inputSimulator.Keyboard.ModifiedKeyStroke(modifierKeyCodes, mainKeyCode);
        }

        #region Helper Methods

        private static (ShortcutInfo, string) GetScalingShortcut()
        {
            try
            {
                string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lossless Scaling", "Settings.xml");
                if (!File.Exists(configPath)) { return (null, $"The configuration file was not found.\nExpected path: {configPath}"); }
                string fileContent = File.ReadAllText(configPath);
                if (string.IsNullOrWhiteSpace(fileContent)) { return (null, "The configuration file 'Settings.xml' is empty."); }
                XDocument doc = XDocument.Parse(fileContent);
                XElement root = doc.Root;
                if (root == null) { return (null, "The configuration file is not a valid XML file."); }
                string keyStr = null;
                string modifiersStr = null;
                XElement hotkeyElement = root.Element("HotKeyMain");
                if (hotkeyElement != null) { keyStr = hotkeyElement.Element("Key")?.Value; modifiersStr = hotkeyElement.Element("Modifiers")?.Value ?? ""; }
                else { keyStr = root.Element("Hotkey")?.Value; modifiersStr = root.Element("HotkeyModifierKeys")?.Value ?? ""; }
                if (string.IsNullOrEmpty(keyStr)) { return (null, "The hotkey could not be found in the XML file."); }
                if (Enum.TryParse(keyStr, true, out Keys key) && key >= Keys.A && key <= Keys.Z)
                {
                    var shortcutInfo = new ShortcutInfo { Key = key, Modifiers = ParseModifiers(modifiersStr) };
                    return (shortcutInfo, null); // Success!
                }
                return (null, $"The read hotkey '{keyStr}' is not a valid letter (A-Z).");
            }
            catch (Exception ex) { return (null, $"An unexpected error occurred while reading the XML file:\n\n{ex.Message}"); }
        }

        private static Modifiers ParseModifiers(string modifiersStr)
        {
            Modifiers mods = Modifiers.None;
            if (string.IsNullOrEmpty(modifiersStr)) return mods;
            var parts = modifiersStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) { switch (part.Trim().ToLower()) { case "control": mods |= Modifiers.Control; break; case "shift": mods |= Modifiers.Shift; break; case "alt": mods |= Modifiers.Alt; break; } }
            return mods;
        }

        private static Process GetOrStartLosslessScalingProcess()
        {
            try
            {
                // First, check if the process is already running.
                Process[] processes = Process.GetProcessesByName(ProcessName);
                if (processes.Length > 0)
                {
                    Console.WriteLine("Lossless Scaling is already running.");
                    return processes[0];
                }

                // If not, we start it.
                if (File.Exists(DefaultInstallPath))
                {
                    Console.WriteLine("Lossless Scaling is being started minimized...");

                    // *** HERE IS THE CHANGE: We are starting the process minimized ***
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = DefaultInstallPath,
                        WindowStyle = ProcessWindowStyle.Minimized // This line ensures it starts minimized.
                    };

                    return Process.Start(startInfo);
                }
                return null;
            }
            catch { return null; }
        }

        private class ShortcutInfo { public Keys Key { get; set; } public Modifiers Modifiers { get; set; } }
        [Flags] private enum Modifiers { None = 0, Alt = 1, Control = 2, Shift = 4 }
        #endregion
    }
}