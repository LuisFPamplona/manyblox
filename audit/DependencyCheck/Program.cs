using System.Text.Json;
using System.Xml.Linq;
using NuGet.Versioning;

// Only the official SDK's NuGet version parser is used. No original packages restored.
var directory = Path.GetFullPath(args.Length == 1 ? args[0] : "audit");
var database = new Dictionary<string, List<Advisory>>(StringComparer.OrdinalIgnoreCase);
foreach (var file in Directory.GetFiles(directory, "nuget-vulnerabilities-*.json"))
    foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, List<Advisory>>>(File.ReadAllText(file))!)
    {
        if (!database.TryGetValue(pair.Key, out var list)) database[pair.Key] = list = [];
        list.AddRange(pair.Value);
    }
var packages = XDocument.Load(Path.Combine(directory, "original-packages.config"));
var results = packages.Root!.Elements("package").Select(p =>
{
    var name = p.Attribute("id")!.Value;
    var version = p.Attribute("version")!.Value;
    var hits = database.GetValueOrDefault(name, []).Where(a => VersionRange.Parse(a.versions).Satisfies(NuGetVersion.Parse(version))).Distinct().ToList();
    return new { Package = name, Version = version, Advisories = hits, Action = "Removed; not restored or executed", Result = hits.Count > 0 ? "Affected version in official feed; exploitability not established" : "No matching advisory in feed; not a safety guarantee" };
}).ToArray();
File.WriteAllText(Path.Combine(directory, "dependency-review.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Reviewed {results.Length} package versions with NuGet SemVer.");
foreach (var p in results.Where(p => p.Advisories.Count > 0)) Console.WriteLine($"{p.Package} {p.Version}: {string.Join(" ; ", p.Advisories.Select(a => a.url))}");
record Advisory(string url, int severity, string versions);
