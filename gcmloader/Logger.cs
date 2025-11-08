using System.IO;
using System.Diagnostics;
using System;

namespace gcmloader
{
    public static class Logger
    {
        // ANGEPASSTER PFAD:
        private static readonly string _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), // AppData\Roaming
            "gcmsettings",                                                        // \gcmsettings
            "gcm_image_debug.log"                                                 // \gcm_image_debug.log
        );

        private static readonly object _lock = new object();

        /// <summary>
        /// Initialisiert den Logger und löscht die alte Log-Datei.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                // Stelle sicher, dass der Ordner existiert
                Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
                File.Delete(_logFilePath);
                Log("Logger initialisiert.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] Fehler beim Initialisieren: {ex.Message}");
            }
        }

        /// <summary>
        /// Schreibt eine Nachricht Thread-sicher in die Log-Datei.
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                // lock stellt sicher, dass nicht mehrere Threads gleichzeitig schreiben
                lock (_lock)
                {
                    // Fügt einen Zeitstempel mit Millisekunden hinzu
                    string logMessage = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
                    File.AppendAllText(_logFilePath, logMessage);
                }
                // Schreibt die Nachricht auch in das Debug-Ausgabefenster von Visual Studio
                Debug.WriteLine($"[ImageDebug] {message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Logger] Kritischer Fehler beim Schreiben des Logs: {ex.Message}");
            }
        }
    }
}