using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace gcmloader
{
    #region Data Model for Shortcut Information

    /// <summary>
    /// Represents data extracted from a Windows shortcut (.lnk) file.
    /// </summary>
    public class ShortcutData
    {
        public string LinkPath { get; set; }
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconLocation { get; set; }
    }

    #endregion

    #region Shortcut Resolver Logic

    /// <summary>
    /// A helper class to resolve Windows shortcuts (.lnk) using P/Invoke with the IShellLink COM interface.
    /// </summary>
    public static class ShortcutResolver
    {
        #region Private COM Imports & Constants

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

        private const uint STGM_READ = 0;
        private const int MAX_PATH = 260;

        #endregion

        #region Public Methods

        /// <summary>
        /// Resolves a .lnk file and returns an object containing its properties.
        /// </summary>
        /// <param name="linkPath">The path to the .lnk file.</param>
        /// <returns>A ShortcutData object with the link's information, or null if an error occurs.</returns>
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
                // Clean up COM objects to prevent memory leaks.
                if (file != null) Marshal.ReleaseComObject(file);
                if (link != null) Marshal.ReleaseComObject(link);
            }
        }

        #endregion
    }

    #endregion
}
