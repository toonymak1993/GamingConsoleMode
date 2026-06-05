using System.Text;

namespace SteamLoader.App.Infrastructure.StoreSync;

internal static class SteamShortcutIds
{
    public static uint ComputeAppId(string appName, string executablePath)
    {
        var combined = QuotePath(executablePath) + appName;
        return ComputeCrc32(Encoding.UTF8.GetBytes(combined)) | 0x80000000u;
    }

    public static string BuildGridId(uint appId)
    {
        return appId.ToString();
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        const uint polynomial = 0xEDB88320u;
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc ^= value;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0
                    ? crc >> 1
                    : (crc >> 1) ^ polynomial;
            }
        }

        return ~crc;
    }

    private static string QuotePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : $"\"{path}\"";
    }
}
