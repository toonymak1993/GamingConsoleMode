using System;
using System.Runtime.InteropServices;

namespace gcmloader
{
    internal static class NativeOpenFileDialog
    {
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        public static string Show(string title, string initialDirectory, string filter)
        {
            var fileBuffer = new string('\0', 1024);
            var titleBuffer = new string('\0', 256);

            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf<OpenFileName>(),
                lpstrFilter = NormalizeFilter(filter),
                lpstrFile = fileBuffer,
                nMaxFile = fileBuffer.Length,
                lpstrFileTitle = titleBuffer,
                nMaxFileTitle = titleBuffer.Length,
                lpstrInitialDir = initialDirectory,
                lpstrTitle = title,
                Flags = 0x00080000 | 0x00001000 | 0x00000800
            };

            return GetOpenFileName(ref ofn) ? ofn.lpstrFile.TrimEnd('\0') : string.Empty;
        }

        private static string NormalizeFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return "All Files (*.*)\0*.*\0\0";
            }

            string normalized = filter.Replace('|', '\0');
            return normalized.EndsWith("\0\0", StringComparison.Ordinal) ? normalized : normalized + "\0\0";
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }
    }
}
