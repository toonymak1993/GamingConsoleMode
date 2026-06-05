using System.Text;

namespace SteamLoader.App.Infrastructure.StoreSync;

public sealed class SteamShortcutFile
{
    private const string ManagedShortcutMarker = "steamloader://managed";

    public IReadOnlyList<Dictionary<string, object?>> Read(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

        if (stream.Length == 0)
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var rootObject = ReadObject(reader);
        if (!rootObject.TryGetValue("shortcuts", out var shortcutsValue) ||
            shortcutsValue is not Dictionary<string, object?> shortcutsObject)
        {
            return Array.Empty<Dictionary<string, object?>>();
        }

        var entries = new List<Dictionary<string, object?>>();

        foreach (var key in shortcutsObject.Keys.OrderBy(ParseNumericKey))
        {
            if (shortcutsObject[key] is Dictionary<string, object?> entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public void Write(string path, IReadOnlyList<Dictionary<string, object?>> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        var shortcuts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entries.Count; index++)
        {
            shortcuts[index.ToString()] = entries[index];
        }

        var root = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["shortcuts"] = shortcuts,
        };

        WriteObject(writer, root);
    }

    public static bool HasManagedTag(Dictionary<string, object?> entry)
    {
        if (entry.TryGetValue("ShortcutPath", out var shortcutPathValue) &&
            shortcutPathValue is string shortcutPath &&
            string.Equals(shortcutPath, ManagedShortcutMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!entry.TryGetValue("tags", out var tagsValue) ||
            tagsValue is not Dictionary<string, object?> tags)
        {
            return false;
        }

        return tags.Values
            .OfType<string>()
            .Any(tag =>
                string.Equals(tag, "Tools for Steam", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "Steam Tools", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "SteamLoader", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, "Store Sync", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> ReadObject(BinaryReader reader)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var type = reader.ReadByte();
            if (type == 0x08)
            {
                break;
            }

            var key = ReadCString(reader);
            result[key] = ReadValue(reader, type);
        }

        return result;
    }

    private static object? ReadValue(BinaryReader reader, byte type)
    {
        return type switch
        {
            0x00 => ReadObject(reader),
            0x01 => ReadCString(reader),
            0x02 => reader.ReadInt32(),
            0x07 => reader.ReadUInt64(),
            _ => throw new InvalidDataException($"Unsupported shortcuts.vdf value type 0x{type:X2}."),
        };
    }

    private static void WriteObject(BinaryWriter writer, Dictionary<string, object?> values)
    {
        foreach (var (key, value) in values)
        {
            WriteKeyValue(writer, key, value);
        }

        writer.Write((byte)0x08);
    }

    private static void WriteKeyValue(BinaryWriter writer, string key, object? value)
    {
        switch (value)
        {
            case Dictionary<string, object?> objectValue:
                writer.Write((byte)0x00);
                WriteCString(writer, key);
                WriteObject(writer, objectValue);
                return;
            case string stringValue:
                writer.Write((byte)0x01);
                WriteCString(writer, key);
                WriteCString(writer, stringValue);
                return;
            case bool boolValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write(boolValue ? 1 : 0);
                return;
            case byte byteValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write((int)byteValue);
                return;
            case short shortValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write((int)shortValue);
                return;
            case int intValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write(intValue);
                return;
            case long longValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write(checked((int)longValue));
                return;
            case uint uintValue:
                writer.Write((byte)0x02);
                WriteCString(writer, key);
                writer.Write(checked((int)uintValue));
                return;
            case ulong ulongValue:
                writer.Write((byte)0x07);
                WriteCString(writer, key);
                writer.Write(ulongValue);
                return;
            case null:
                writer.Write((byte)0x01);
                WriteCString(writer, key);
                WriteCString(writer, string.Empty);
                return;
            default:
                writer.Write((byte)0x01);
                WriteCString(writer, key);
                WriteCString(writer, Convert.ToString(value) ?? string.Empty);
                return;
        }
    }

    private static string ReadCString(BinaryReader reader)
    {
        using var memory = new MemoryStream();

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                break;
            }

            memory.WriteByte(value);
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static void WriteCString(BinaryWriter writer, string value)
    {
        writer.Write(Encoding.UTF8.GetBytes(value));
        writer.Write((byte)0x00);
    }

    private static int ParseNumericKey(string key)
    {
        return int.TryParse(key, out var value) ? value : int.MaxValue;
    }
}
