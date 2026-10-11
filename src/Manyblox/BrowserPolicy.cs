namespace Manyblox;

public static class BrowserPolicy
{
    // Top-level pages stay on the official Roblox website. Subresources are
    // fetched by the browser, including Roblox's CDN and verification providers.
    public static bool IsOfficialPage(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        uri.UserInfo.Length == 0 &&
        (uri.IdnHost == "www.roblox.com" || uri.IdnHost == "roblox.com");

    public static bool CanLaunchClient(string value, string origin, string source, bool userInitiated) =>
        userInitiated && IsOfficialPage(origin) && IsOfficialPage(source) &&
        value.Length <= 16384 && !value.Any(c => char.IsControl(c) || c is '"' or '<' or '>') &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "roblox-player" || uri.Scheme == "roblox");

    public static bool IsGamePage(string value, long placeId)
    {
        if (placeId <= 0 || !IsOfficialPage(value)) return false;
        var uri = new Uri(value);
        string path = "/games/" + placeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return uri.AbsolutePath == path || uri.AbsolutePath.StartsWith(path + "/", StringComparison.Ordinal);
    }
}
