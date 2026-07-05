#if PFP_DEVTOOLS
using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace Clarion.Services;

/// <summary>
/// Dev tool: logs every addon as it opens/closes, so a modal picker (which blocks command input
/// while it's on screen) can be identified without needing to run a command while it's up.
/// </summary>
public sealed class AddonWatcher : IDisposable
{
    private bool watching;
    public bool Watching => watching;

    public void Toggle()
    {
        if (watching)
            Stop();
        else
            Start();
    }

    private void Start()
    {
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, OnEvent);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnEvent);
        watching = true;
        Plugin.Log.Information("[PFP] Addon watch ON. Open the specific-duty selector now; its name will be logged.");
    }

    private void Stop()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnEvent);
        watching = false;
        Plugin.Log.Information("[PFP] Addon watch OFF.");
    }

    private void OnEvent(AddonEvent type, AddonArgs args)
    {
        Plugin.Log.Information($"[PFP] Addon {(type == AddonEvent.PostSetup ? "opened" : "closing")}: {args.AddonName}");
    }

    public void Dispose()
    {
        if (watching)
            Stop();
    }
}
#endif
