using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Plugin;

namespace JobstoneNecklaceSwitcher;

public sealed class PenumbraBridge
{
    private readonly IDalamudPluginInterface _pi;
    public PenumbraBridge(IDalamudPluginInterface pi) => _pi = pi;

    public string[] GetCollectionNames() => GetCollectionNamesWithDebug(out _);

    public string[] GetCollectionNamesWithDebug(out string debug)
    {
        var dbg = new List<string>();

        // 1) Try Penumbra IPC (may be NotReady during login)
        foreach (var ch in new[] { "Penumbra.GetCollections", "Penumbra.Api.GetCollections", "Penumbra.CollectionNames" })
        {
            try
            {
                var sub = _pi.GetIpcSubscriber<string[]>(ch);
                var arr = sub.InvokeFunc();
                dbg.Add($"IPC {ch}: {(arr is { Length: > 0 } ? arr.Length : 0)}");
                if (arr is { Length: > 0 })
                {
                    debug = string.Join(" | ", dbg);
                    return WithHeader(arr.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToArray());
                }
            }
            catch (Exception ex)
            {
                dbg.Add($"IPC {ch} error: {ex.GetType().Name}");
            }
        }

        // 2) File-based discovery
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesRead = 0;

        foreach (var dir in CandidateConfigDirs())
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    dbg.Add($"Dir missing: {dir}");
                    continue;
                }

                // New layout: Penumbra/collections/*.json
                var colDir = Path.Combine(dir, "collections");
                if (Directory.Exists(colDir))
                {
                    var files = Directory.EnumerateFiles(colDir, "*.json", SearchOption.TopDirectoryOnly).ToArray();
                    filesRead += files.Length;
                    dbg.Add($"Scan {colDir} -> {files.Length} files");
                    foreach (var f in files)
                        TryAddNameFromFile(f, names, dbg);
                }

                // Legacy single file: Penumbra/collections.json
                var legacy = Path.Combine(dir, "collections.json");
                if (File.Exists(legacy))
                {
                    filesRead++;
                    dbg.Add($"Scan legacy {legacy}");
                    ExtractFromLegacyCollections(legacy, names, dbg);
                }
            }
            catch (Exception ex)
            {
                dbg.Add($"Scan error {dir}: {ex.GetType().Name}");
            }
        }

        dbg.Add($"Total files read: {filesRead} | names: {names.Count}");
        debug = string.Join(" | ", dbg);

        if (names.Count > 0)
            return WithHeader(names.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray());

        return new[] { "— choose one —" };
    }

    private static IEnumerable<string> CandidateConfigDirs()
    {
        // Prefer the real Linux HOME; Wine may report C:\users\<name>
        var envHome  = Environment.GetEnvironmentVariable("HOME");
        var user     = Environment.UserName;
        var linuxHome = $"/home/{user}";

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(envHome))
            candidates.Add(Path.Combine(envHome, ".xlcore", "pluginConfigs", "Penumbra"));

        candidates.Add(Path.Combine(linuxHome, ".xlcore", "pluginConfigs", "Penumbra"));

        // Also scan Core wineprefix(es)
        var wineRoots = new[]
        {
            Path.Combine(linuxHome, ".xlcore", "wineprefix"),
            string.IsNullOrWhiteSpace(envHome) ? null : Path.Combine(envHome!, ".xlcore", "wineprefix"),
        }.Where(p => !string.IsNullOrWhiteSpace(p))!;

        foreach (var wr in wineRoots)
        {
            try
            {
                if (!Directory.Exists(wr)) continue;
                foreach (var d in Directory.EnumerateDirectories(wr!, "Penumbra", SearchOption.AllDirectories))
                {
                    var norm = d.Replace('\\', '/');
                    if (norm.EndsWith("/pluginConfigs/Penumbra"))
                        candidates.Add(d);
                }
            }
            catch
            {
                // ignore
            }
        }

        // De-dupe
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
            if (seen.Add(c))
                yield return c;
    }

    private static void TryAddNameFromFile(string path, HashSet<string> names, List<string> dbg)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Name", out var n) &&
                n.ValueKind == JsonValueKind.String)
            {
                var name = n.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name!);
                    dbg.Add($"Name from {Path.GetFileName(path)}: {name}");
                }
            }
            else
            {
                ScanForNames(root, names, dbg, Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            dbg.Add($"Read error {Path.GetFileName(path)}: {ex.GetType().Name}");
        }
    }

    private static void ExtractFromLegacyCollections(string path, HashSet<string> names, List<string> dbg)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Collections", out var col) &&
                col.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in col.EnumerateObject())
                {
                    if (!string.IsNullOrWhiteSpace(p.Name))
                    {
                        names.Add(p.Name);
                        dbg.Add($"Legacy key: {p.Name}");
                    }
                }
            }
            else
            {
                ScanForNames(root, names, dbg, Path.GetFileName(path));
            }
        }
        catch (Exception ex)
        {
            dbg.Add($"Legacy error {Path.GetFileName(path)}: {ex.GetType().Name}");
        }
    }

    private static void ScanForNames(JsonElement e, HashSet<string> names, List<string> dbg, string src)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.NameEquals("Name") && p.Value.ValueKind == JsonValueKind.String)
                    {
                        var name = p.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            names.Add(name!);
                            dbg.Add($"Name (scan) {src}: {name}");
                        }
                    }
                    else
                    {
                        ScanForNames(p.Value, names, dbg, src);
                    }
                }
                break;

            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray())
                    ScanForNames(x, names, dbg, src);
                break;
        }
    }

    private static string[] WithHeader(IEnumerable<string> names)
        => new[] { "— choose one —" }.Concat(names).ToArray();
}
