using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Clarion.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private Configuration Config => plugin.Config;

    public ConfigWindow(Plugin plugin) : base("Clarion Settings##pfp_cfg")
    {
        this.plugin = plugin;
        Size = new Vector2(460, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var enabled = Config.Enabled;
        if (ImGui.Checkbox("Plugin enabled", ref enabled))
        {
            Config.Enabled = enabled;
            Config.Save();
        }

        ImGui.Separator();
        ImGui.TextDisabled("Auto-refresh");

        var autoRefresh = Config.AutoRefreshEnabled;
        if (ImGui.Checkbox("Keep my listing alive", ref autoRefresh))
        {
            Config.AutoRefreshEnabled = autoRefresh;
            Config.Save();
            // Enabling it while a listing is already live: refresh straight away to reset its clock and
            // anchor exact timing (covers turning this on partway through an hour). Gated on the
            // recruiting flag so enabling with no listing up never opens the PF.
            if (autoRefresh && Services.AutoRefreshService.IsRecruiting)
                plugin.AutoRefresh.StartRefresh();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Re-confirms your active Party Finder listing near the end of its hour so it doesn't expire.");

        if (Config.AutoRefreshEnabled)
        {
            ImGui.SetNextItemWidth(120);
            var remaining = Config.RefreshWhenMinutesRemaining;
            if (ImGui.InputInt("Refresh when minutes remain", ref remaining))
            {
                Config.RefreshWhenMinutesRemaining = Math.Clamp(remaining, 1, 59);
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Minutes left on the listing's hour when it refreshes. 5 refreshes it just before it would expire.");
        }

#if PFP_DEVTOOLS
        ImGui.Separator();
        ImGui.TextDisabled("Developer build");

        var verbose = Config.VerboseLogging;
        if (ImGui.Checkbox("Verbose logging", ref verbose))
        {
            Config.VerboseLogging = verbose;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Mirror preset apply/read details to the Dalamud log (/xllog) so behaviour can be watched live. Dev builds only.");
#endif
    }
}
