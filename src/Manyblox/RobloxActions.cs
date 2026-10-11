using System.Diagnostics;
using System.Globalization;

namespace Manyblox;

public static class RobloxActions
{
    public static Uri LoginUri => new("https://www.roblox.com/login");
    public static Uri ProfileUri(long id) => new("https://www.roblox.com/users/" + PositiveId(id) + "/profile");
    public static Uri GameUri(long id) => new("https://www.roblox.com/games/" + PositiveId(id));
    public static Uri LaunchUri(long id) => new("roblox://experiences/start?placeId=" + PositiveId(id));

    private static string PositiveId(long id) => id > 0
        ? id.ToString(CultureInfo.InvariantCulture) : throw new InvalidDataException("ID inválido.");

    // Only URIs constructed by the methods above; never user-supplied executable paths/arguments.
    public static void Open(Uri uri)
    {
        if (!IsAllowed(uri))
            throw new InvalidDataException("Destino não permitido.");
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }

    public static void ForwardOfficialClientRequest(string value, string origin, string source, bool authorized)
    {
        if (!BrowserPolicy.CanLaunchClient(value, origin, source, authorized))
            throw new InvalidDataException("Solicitação de lançamento não permitida.");
        // Forward only the official website's request to the installed protocol
        // handler. Never persist or include its short-lived ticket in diagnostics.
        Process.Start(new ProcessStartInfo(value) { UseShellExecute = true })?.Dispose();
    }

    public static bool IsAllowed(Uri uri)
    {
        if (!uri.IsAbsoluteUri) return false;
        string value = uri.AbsoluteUri;
        if (value == LoginUri.AbsoluteUri) return true;
        foreach (var (prefix, suffix) in new[]
        {
            ("https://www.roblox.com/users/", "/profile"),
            ("https://www.roblox.com/games/", ""),
            ("roblox://experiences/start?placeId=", "")
        })
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(suffix, StringComparison.Ordinal)) continue;
            if (value.Length <= prefix.Length + suffix.Length) return false;
            string id = value.Substring(prefix.Length, value.Length - prefix.Length - suffix.Length);
            try { return Rules.ParseId(id).ToString(CultureInfo.InvariantCulture) == id; }
            catch (InvalidDataException) { return false; }
        }
        return false;
    }
}

public sealed record InstanceEntry(int Pid, DateTime Started, string Title);

public static class InstanceMonitor
{
    public static List<InstanceEntry> List()
    {
        var result = new List<InstanceEntry>();
        foreach (var p in Process.GetProcessesByName("RobloxPlayerBeta"))
        {
            using (p)
                try { if (!p.HasExited && p.MainWindowHandle != IntPtr.Zero) result.Add(new(p.Id, p.StartTime, p.MainWindowTitle)); }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        return result;
    }

    public static bool RequestClose(InstanceEntry entry)
    {
        using var p = Process.GetProcessById(entry.Pid);
        if (p.ProcessName != "RobloxPlayerBeta" || p.StartTime != entry.Started || p.HasExited)
            throw new InvalidOperationException("A instância mudou. Atualize a lista.");
        return p.CloseMainWindow();
    }
}
