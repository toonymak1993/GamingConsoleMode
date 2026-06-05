namespace SteamLoader.App.Infrastructure.Assets;

internal static class EmbeddedAssetReader
{
    public static string ReadText(string relativePath)
    {
        var assembly = typeof(EmbeddedAssetReader).Assembly;
        var resourceSuffix = relativePath.Replace('\\', '.').Replace('/', '.');
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

        using var stream = resourceName is not null
            ? assembly.GetManifestResourceStream(resourceName)
            : null;

        if (stream is null)
        {
            throw new FileNotFoundException($"Embedded asset not found: {relativePath}", resourceSuffix);
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
