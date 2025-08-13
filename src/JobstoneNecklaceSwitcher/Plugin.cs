using System;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ImGuiNET;

namespace JobstoneNecklaceSwitcher;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Jobstone Necklace Auto-Switcher";

    [PluginService] public static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;

    private readonly PenumbraBridge _penumbra;
    private readonly GameStateWatcher _watcher;
    internal readonly PluginConfig Config;

    private string[]? _collections;
    private int _collectionIndex = 0;
    private bool _showConfig = false;

    private const string Cmd = "/jsneck";

    public Plugin()
    {
        Config = Pi.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        Config.Initialize(Pi);
        SeedDefaultsIfNeeded();

        _penumbra = new PenumbraBridge(Pi);
        _watcher  = new GameStateWatcher(Framework, ClientState, Config);

        Commands.AddHandler(Cmd, new CommandInfo(OnCommand) { HelpMessage = "Open Jobstone Necklace Auto-Switcher configuration." });

        Pi.UiBuilder.Draw += DrawUi;
        Pi.UiBuilder.OpenConfigUi += () => _showConfig = true;
    }

    public void Dispose()
    {
        Pi.UiBuilder.Draw -= DrawUi;
        Commands.RemoveHandler(Cmd);
        _watcher.Dispose();
    }

    private void OnCommand(string command, string args) => _showConfig = true;

    private void DrawUi()
    {
        if (!_showConfig) return;

        ImGui.SetNextWindowSize(new Vector2(560, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Jobstone Necklace Auto-Switcher", ref _showConfig, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        // locals used throughout the window
        string dbg = string.Empty;
        var cols = _collections ?? Array.Empty<string>();

        if (ImGui.BeginTabBar("jsneck_tabs"))
        {
            // ===== General Tab =====
            if (ImGui.BeginTabItem("General"))
            {
                bool enabled = Config.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    Config.Enabled = enabled;
                    Config.Save();
                }

                // Ensure we have a first-time list
                if (_collections == null || _collections.Length <= 1)
                {
                    _collections = _penumbra.GetCollectionNamesWithDebug(out dbg);
                    cols = _collections ?? Array.Empty<string>();
                }

                ImGui.TextUnformatted("Target Penumbra Collection");
                ImGui.SameLine();
                if (ImGui.SmallButton("↻ Refresh"))
                {
                    _collections = _penumbra.GetCollectionNamesWithDebug(out dbg);
                    cols = _collections ?? Array.Empty<string>();
                }
                ImGui.SameLine();
                ImGui.TextDisabled($"(found {(cols.Length > 0 ? cols.Length - 1 : 0)})");

                if (!string.IsNullOrEmpty(dbg))
                    ImGui.TextDisabled(dbg);

                // Determine current index
                if (Config.IsConfigured)
                {
                    _collectionIndex = 0;
                    for (int i = 0; i < cols.Length; i++)
                    {
                        if (string.Equals(cols[i], Config.TargetCollection, StringComparison.Ordinal))
                        {
                            _collectionIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    _collectionIndex = 0;
                }

                string current = Config.IsConfigured ? Config.TargetCollection : "— choose one —";
                if (ImGui.BeginCombo("##collection", current))
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        bool sel = i == _collectionIndex;
                        if (ImGui.Selectable(cols[i], sel))
                        {
                            _collectionIndex = i;
                            Config.TargetCollection = (i == 0) ? "__CHOOSE__" : cols[i];
                            Config.Save();
                        }
                        if (sel) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }

                bool keep = Config.KeepCurrentStoneAfterChangingClass;
                if (ImGui.Checkbox("Keep Current Stone After Changing Class", ref keep))
                {
                    Config.KeepCurrentStoneAfterChangingClass = keep;
                    Config.Save();
                }

                bool glow = Config.EnableGlow;
                if (ImGui.Checkbox("Enable Glow (if available)", ref glow))
                {
                    Config.EnableGlow = glow;
                    Config.Save();
                }

                ImGui.Separator();
                if (!Config.IsConfigured)
                    ImGui.TextWrapped("Select a Penumbra collection to enable auto-switching.");
                else
                    ImGui.TextWrapped($"Using collection: {Config.TargetCollection}");

                ImGui.EndTabItem();
            }

            // ===== Mapping Tab =====
            if (ImGui.BeginTabItem("Mapping"))
            {
                ImGui.TextUnformatted("Auto-generated from defaults; advanced users can edit the config file.");
                ImGui.Separator();

                ImGui.Columns(3, "mapcols", true);
                ImGui.Text("Job"); ImGui.NextColumn();
                ImGui.Text("Group"); ImGui.NextColumn();
                ImGui.Text("Option"); ImGui.NextColumn();
                ImGui.Separator();

                foreach (var kv in Config.Mappings)
                {
                    ImGui.TextUnformatted(kv.Key); ImGui.NextColumn();
                    ImGui.TextUnformatted(kv.Value.Group); ImGui.NextColumn();
                    ImGui.TextUnformatted(kv.Value.Pendant); ImGui.NextColumn();
                }
                ImGui.Columns(1);

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void SeedDefaultsIfNeeded()
    {
        try
        {
            if (Config.Mappings != null && Config.Mappings.Count > 0) return;

            var asm = Assembly.GetExecutingAssembly();
            using var s = asm.GetManifestResourceStream("JobstoneNecklaceSwitcher.DefaultMappings.json");
            if (s == null) return;

            using var doc = JsonDocument.Parse(s);
            var root = doc.RootElement;
            var group = root.TryGetProperty("Group", out var g) && g.ValueKind == JsonValueKind.String ? g.GetString()! : "Textures";
            var glowDefault = root.TryGetProperty("EnableGlowDefault", out var eg) && eg.ValueKind == JsonValueKind.True;

            if (root.TryGetProperty("Jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in jobs.EnumerateObject())
                {
                    var job = prop.Name;
                    var opt = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(opt))
                        Config.Mappings[job] = (group, opt);
                }
            }

            if (!Config.EnableGlow) Config.EnableGlow = glowDefault;
            Config.Save();
        }
        catch
        {
            // swallow
        }
    }
}
