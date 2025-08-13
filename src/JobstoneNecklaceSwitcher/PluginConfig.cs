using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace JobstoneNecklaceSwitcher;

[Serializable]
public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled = true;
    public bool KeepCurrentStoneAfterChangingClass = false;

    // Set via dropdown; must be non-empty and not "__CHOOSE__"
    public string TargetCollection = "";

    public bool EnableGlow = true;

    // Job -> (Group, Option) mapping
    public Dictionary<string, (string Group, string Pendant)> Mappings = new();

    [NonSerialized] private IDalamudPluginInterface? _pi;

    public void Initialize(IDalamudPluginInterface pi) => _pi = pi;

    public void Save() => _pi?.SavePluginConfig(this);

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TargetCollection)
        && TargetCollection != "__CHOOSE__";
}
