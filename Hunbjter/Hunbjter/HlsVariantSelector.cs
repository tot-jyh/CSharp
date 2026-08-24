namespace Hunbjter;

public static class HlsVariantSelector
{
    public static string SelectBestVariantUrl(string playlistUrl, string playlistContent)
    {
        var variants = ParseVariants(playlistUrl, playlistContent);
        return variants
            .OrderByDescending(variant => variant.Height)
            .ThenByDescending(variant => variant.Width)
            .ThenByDescending(variant => variant.Bandwidth)
            .FirstOrDefault()
            ?.Url ?? playlistUrl;
    }

    private static List<HlsVariant> ParseVariants(string playlistUrl, string playlistContent)
    {
        var variants = new List<HlsVariant>();
        var lines = playlistContent.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i];
            if (!line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextLine = lines[i + 1];
            if (nextLine.StartsWith('#'))
            {
                continue;
            }

            var bandwidth = ExtractIntAttribute(line, "BANDWIDTH");
            var resolution = ExtractResolution(line);
            variants.Add(new HlsVariant(
                ResolveUrl(playlistUrl, nextLine),
                bandwidth,
                resolution.Width,
                resolution.Height));
        }

        return variants;
    }

    private static int ExtractIntAttribute(string line, string name)
    {
        var marker = $"{name}=";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return 0;
        }

        start += marker.Length;
        var end = line.IndexOf(',', start);
        var value = end < 0 ? line[start..] : line[start..end];
        return int.TryParse(value.Trim(), out var number) ? number : 0;
    }

    private static (int Width, int Height) ExtractResolution(string line)
    {
        var marker = "RESOLUTION=";
        var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return (0, 0);
        }

        start += marker.Length;
        var end = line.IndexOf(',', start);
        var value = end < 0 ? line[start..] : line[start..end];
        var parts = value.Split('x');
        return parts.Length == 2
            && int.TryParse(parts[0], out var width)
            && int.TryParse(parts[1], out var height)
            ? (width, height)
            : (0, 0);
    }

    private static string ResolveUrl(string playlistUrl, string variantUrl)
    {
        return Uri.TryCreate(variantUrl, UriKind.Absolute, out var absolute)
            ? absolute.ToString()
            : new Uri(new Uri(playlistUrl), variantUrl).ToString();
    }

    private sealed record HlsVariant(string Url, int Bandwidth, int Width, int Height);
}
