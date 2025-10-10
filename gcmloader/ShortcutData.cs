using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace gcmloader
{
    /// <summary>
    /// Stellt Daten dar, die aus einer Windows-Verknüpfungsdatei (.lnk) extrahiert wurden.
    /// </summary>
    public class ShortcutData
    {
        public string LinkPath { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconLocation { get; set; }
    }

    /// <summary>
    /// Eine Hilfsklasse zum Auflösen von Windows-Verknüpfungen (.lnk) unter Verwendung von P/Invoke mit der IShellLink-Schnittstelle.
    /// </summary>
    public static class ShortcutResolver
    {
        #region COM Imports
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        [ClassInterface(ClassInterfaceType.None)]
        private class ShellLink { }

        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLink
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            void IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
        #endregion

        private const uint STGM_READ = 0;
        private const int MAX_PATH = 260;

        /// <summary>
        /// Löst eine .lnk-Datei auf und gibt ein Objekt mit ihren Eigenschaften zurück.
        /// </summary>
        /// <param name="linkPath">Der Pfad zur .lnk-Datei.</param>
        /// <returns>Ein ShortcutData-Objekt oder null bei einem Fehler.</returns>
        public static ShortcutData Resolve(string linkPath)
        {
            if (!File.Exists(linkPath) || !linkPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            IShellLink link = null;
            IPersistFile file = null;
            try
            {
                link = (IShellLink)new ShellLink();
                file = (IPersistFile)link;
                file.Load(linkPath, STGM_READ);

                var targetPath = new StringBuilder(MAX_PATH);
                var arguments = new StringBuilder(MAX_PATH);
                var workingDir = new StringBuilder(MAX_PATH);
                var iconLocation = new StringBuilder(MAX_PATH);

                link.GetPath(targetPath, targetPath.Capacity, IntPtr.Zero, 0);
                link.GetArguments(arguments, arguments.Capacity);
                link.GetWorkingDirectory(workingDir, workingDir.Capacity);
                link.GetIconLocation(iconLocation, iconLocation.Capacity, out _);

                return new ShortcutData
                {
                    LinkPath = linkPath,
                    TargetPath = targetPath.ToString(),
                    Arguments = arguments.ToString(),
                    WorkingDirectory = workingDir.ToString(),
                    IconLocation = iconLocation.ToString()
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShortcutResolver] Error resolving shortcut '{linkPath}': {ex.Message}");
                return null;
            }
            finally
            {
                if (file != null) Marshal.ReleaseComObject(file);
                if (link != null) Marshal.ReleaseComObject(link);
            }
        }
    }
}