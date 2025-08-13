using System;
using Dalamud.Plugin.Services;

namespace JobstoneNecklaceSwitcher;

public sealed class GameStateWatcher : IDisposable
{
    private readonly PluginConfig config;
    private readonly IFramework framework;
    private readonly IClientState client;

    private uint lastJobId = 0;

    public GameStateWatcher(IFramework framework, IClientState client, PluginConfig config)
    {
        this.framework = framework;
        this.client = client;
        this.config = config;

        this.framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        this.framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!config.Enabled || !config.IsConfigured) return;

        var local = client.LocalPlayer;
        if (local == null) return;

        // Lumina RowRef<ClassJob> -> RowId is the numeric job id
        var jobId = local.ClassJob.RowId;
        if (jobId == lastJobId) return;
        lastJobId = jobId;

        // TODO: apply mapping to Penumbra here
    }
}
