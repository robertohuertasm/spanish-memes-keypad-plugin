namespace Loupedeck.SpanishMemesPlugin;

using System.Net;
using System.Text.RegularExpressions;

internal static class TrendingCatalog
{
    private static readonly Uri BaseUri = new("https://www.myinstants.com/");
    private static readonly Regex SoundPath = new("""play\(\s*['"](?<path>/media/sounds/[^'"]+)['"]""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static Uri[] Parse(string html) => SoundPath.Matches(WebUtility.HtmlDecode(html))
        .Select(match => Uri.TryCreate(BaseUri, match.Groups["path"].Value, out var uri) ? uri : null)
        .Where(uri => uri is not null && uri.Scheme == Uri.UriSchemeHttps && uri.Host == BaseUri.Host && uri.AbsolutePath.StartsWith("/media/sounds/", StringComparison.Ordinal))
        .Select(uri => uri!)
        .Distinct()
        .ToArray();
}
