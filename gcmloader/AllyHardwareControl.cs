using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices; // Wichtig für die neuen DLL-Imports
using HidSharp;
using HidSharp.Reports;

namespace GAMINGCONSOLEMODE
{
    public static class AllyHardwareControl
    {
        private const int ASUS_VID = 0x0B05;
        private const int ALLY_PID = 0x1ABE;
        private const byte INPUT_ID = 0x5A;

        private static CancellationTokenSource _cts;
        private static DateTime _lastActionTime = DateTime.MinValue;

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "gcmsettings",
            "ally_debug.txt");

        private static void Log(string message)
        {
            try
            {
                string dir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        // --- VERBESSERTE WIN32 API (SCAN CODES) ---
        [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);

        private const int KEYEVENTF_KEYUP = 0x0002;
        // Tasten
        private const byte VK_LCONTROL = 0xA2;
        private const byte VK_1 = 0x31;
        private const byte VK_2 = 0x32;
        private const byte VK_LSHIFT = 0xA0;
        private const byte VK_TAB = 0x09;
        private const byte VK_LWIN = 0x5B;
        private const byte VK_ESCAPE = 0x1B;

        // Listener Trigger
        private const int VK_MULTIPLY = 0x6A; // Numpad *
        private const int VK_DIVIDE = 0x6F;   // Numpad /

        public static void InitializeAllyButtons()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try { if (File.Exists(LogPath)) File.Delete(LogPath); } catch { }
            Log("=== GCM Ally V35 (Scan Code Fix) ===");

            Task.Run(async () =>
            {
                KillAsusServices();

                var device = FindCorrectDevice();
                if (device != null)
                {
                    Log($"Gerät gefunden: {device.DevicePath}");

                    if (WriteInput(device, Encoding.ASCII.GetBytes("ZASUS Tech.Inc."), "WakeUp"))
                    {
                        await Task.Delay(200);

                        // 1. AUTO MODE
                        WriteInput(device, new byte[] { INPUT_ID, 0xD1, 0x01, 0x01, 0x00 }, "SetMode(Auto)");
                        await Task.Delay(200);

                        // 2. Ready
                        WriteInput(device, new byte[] { INPUT_ID, 0xD1, 0x0A, 0x01 }, "ReadyCmd");
                        await Task.Delay(100);

                        // 3. BINDINGS (NumPad / und *)
                        byte[] bindings = new byte[50];
                        new byte[] { INPUT_ID, 0xD1, 0x02, 0x08, 0x2C }.CopyTo(bindings, 0);

                        // M2 -> Num *
                        CreateKeyStruct(0x02, 0x7C).CopyTo(bindings, 5);
                        // M1 -> Num /
                        CreateKeyStruct(0x02, 0x90).CopyTo(bindings, 27);

                        WriteInput(device, bindings, "SetBindings(NumPad)");
                        await Task.Delay(100);

                        // 4. Save
                        WriteInput(device, new byte[] { INPUT_ID, 0xD1, 0x0F, 0x20 }, "Save");

                        Log(">>> READY. Shortcuts mit Scan-Codes aktiv. <<<");
                        StartKeyboardHook(_cts.Token);
                    }
                }
                else
                {
                    Log("FEHLER: Device nicht gefunden.");
                }
            });
        }

        private static void KillAsusServices()
        {
            string[] services = { "ArmouryCrate.Service", "ArmouryCrate.UserSessionHelper", "ASUSOptimization" };
            foreach (var s in services)
            {
                try { foreach (var proc in Process.GetProcessesByName(s)) proc.Kill(); } catch { }
            }
        }

        private static HidDevice FindCorrectDevice()
        {
            return DeviceList.Local.GetHidDevices(ASUS_VID)
                .FirstOrDefault(d => d.ProductID == ALLY_PID &&
                                     d.GetReportDescriptor().TryGetReport(ReportType.Feature, INPUT_ID, out _));
        }

        private static byte[] CreateKeyStruct(byte type, byte keycode)
        {
            byte[] code = new byte[10];
            code[0] = type; code[2] = keycode; return code;
        }

        private static bool WriteInput(HidDevice device, byte[] data, string logName)
        {
            try
            {
                using (var stream = device.Open())
                {
                    int len = device.MaxFeatureReportLength;
                    var payload = new byte[len];
                    Array.Copy(data, payload, Math.Min(data.Length, len));
                    stream.SetFeature(payload);
                    return true;
                }
            }
            catch (Exception ex) { Log($" -> {logName} FEHLER: {ex.Message}"); return false; }
        }

        private static void StartKeyboardHook(CancellationToken token)
        {
            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    if (IsKeyDown(VK_MULTIPLY)) // M2
                    {
                        Log(">> INPUT: M2");
                        TriggerAction("M2");
                        while (IsKeyDown(VK_MULTIPLY)) await Task.Delay(50);
                    }
                    if (IsKeyDown(VK_DIVIDE)) // M1
                    {
                        Log(">> INPUT: M1");
                        TriggerAction("M1");
                        while (IsKeyDown(VK_DIVIDE)) await Task.Delay(50);
                    }
                    await Task.Delay(10);
                }
            });
        }

        private static bool IsKeyDown(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }

        private static void TriggerAction(string buttonName)
        {
            if ((DateTime.Now - _lastActionTime).TotalMilliseconds < 300) return;
            _lastActionTime = DateTime.Now;

            string action = "None";
            // Normalerweise aus Config laden, hier Hardcoded für deinen Wunsch:
            if (buttonName == "M1") action = "Steam"; // Soll Strg+1 sein
            if (buttonName == "M2") action = "Steam Sidepanel"; // Soll Strg+2 sein

            // Falls du die Config-Datei hast, lade sie hier:
            try
            {
                string key = buttonName == "M1" ? "rog_m1_action" : "rog_m2_action";
                string loaded = AppSettings.Load<string>(key);
                if (!string.IsNullOrEmpty(loaded)) action = loaded;
            }
            catch { }

            Log($"[EXECUTE] {buttonName} -> {action}");

            switch (action)
            {
                case "Steam":
                    SendKeyCombination(VK_LCONTROL, VK_1);
                    break;
                case "Steam Sidepanel":
                    SendKeyCombination(VK_LCONTROL, VK_2);
                    break;
                case "Task Manager":
                    SendThreeKeys(VK_LCONTROL, VK_LSHIFT, VK_ESCAPE);
                    break;
                case "Switch Tab":
                    SendKeyCombination(VK_LWIN, VK_TAB);
                    break;
            }
        }

        // --- NEUE SIMULATION MIT SCAN CODES ---
        private static void SendKeyCombination(byte key1, byte key2)
        {
            // Wir holen den "echten" Hardware-Scan-Code
            byte scan1 = (byte)MapVirtualKey(key1, 0);
            byte scan2 = (byte)MapVirtualKey(key2, 0);

            // Drücken (mit Scan Code!)
            keybd_event(key1, scan1, 0, UIntPtr.Zero);
            keybd_event(key2, scan2, 0, UIntPtr.Zero);

            Thread.Sleep(100); // Etwas Zeit lassen

            // Loslassen
            keybd_event(key2, scan2, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(key1, scan1, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static void SendThreeKeys(byte key1, byte key2, byte key3)
        {
            byte scan1 = (byte)MapVirtualKey(key1, 0);
            byte scan2 = (byte)MapVirtualKey(key2, 0);
            byte scan3 = (byte)MapVirtualKey(key3, 0);

            keybd_event(key1, scan1, 0, UIntPtr.Zero);
            keybd_event(key2, scan2, 0, UIntPtr.Zero);
            keybd_event(key3, scan3, 0, UIntPtr.Zero);

            Thread.Sleep(100);

            keybd_event(key3, scan3, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(key2, scan2, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(key1, scan1, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}