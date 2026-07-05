using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Clarion.Model;

namespace Clarion;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Mirror apply/read diagnostics to the Dalamud log. Only consulted by dev builds (the logging
    /// call sites are compiled out entirely when PFP_DEVTOOLS is not defined), so this has no effect
    /// in a shipped release.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>Captured AtkValue arrays from the diagnostic tool, kept across sessions so field mapping work can span multiple play sessions.</summary>
    public List<CapturedEntry> DiagnosticCaptures { get; set; } = new();

    public List<PartyFinderPreset> Presets { get; set; } = new();

    // ── Auto-refresh ────────────────────────────────────────────────────────
    /// <summary>Keep an active Party Finder listing alive by re-confirming it near the end of its hour.</summary>
    public bool AutoRefreshEnabled { get; set; } = false;

    /// <summary>
    /// Refresh the listing once this many minutes are left on its hour - matching the countdown the
    /// player sees in the listing. Default 5 refreshes it just before it would expire.
    /// </summary>
    public int RefreshWhenMinutesRemaining { get; set; } = 5;

    /// <summary>One-time notice about needing the plugin running before you post has been shown.</summary>
    public bool AutoRefreshNoticeShown { get; set; } = false;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi) => pluginInterface = pi;

    public void Save() => pluginInterface?.SavePluginConfig(this);
}

[Serializable]
public class CapturedEntry
{
    public DateTime CapturedAt { get; set; }
    public string Label { get; set; } = string.Empty;
    public string AddonName { get; set; } = string.Empty;
    public List<CapturedValue> Values { get; set; } = new();
}

[Serializable]
public class CapturedValue
{
    public int Index { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
