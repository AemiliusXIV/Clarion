using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Clarion.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private Configuration Config => plugin.Config;

    public ConfigWindow(Plugin plugin) : base($"Clarion Settings{Plugin.BuildTag}##pfp_cfg")
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
        ImGui.SameLine();
        if (ImGui.Button("Presets"))
            plugin.OpenMain();

        var autoShow = Config.AutoShowWithRecruitmentForm;
        if (ImGui.Checkbox("Quick pop-up with the recruitment form", ref autoShow))
        {
            Config.AutoShowWithRecruitmentForm = autoShow;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Show a small quick-apply pop-up next to the Recruitment Criteria form while it's open (only when the full window isn't already open).");

        if (Config.AutoShowWithRecruitmentForm)
        {
            var topLeft = Config.CompanionAnchorTopLeft;
            if (ImGui.Checkbox("Pin the pop-up to the form's top-left", ref topLeft))
            {
                Config.CompanionAnchorTopLeft = topLeft;
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("On: the pop-up sits at the form's top-left. Off: it's centered vertically on the form (middle-left).");
        }

        ImGui.Separator();
        ImGui.TextDisabled("Auto-refresh");

        var autoRefresh = Config.AutoRefreshEnabled;
        if (ImGui.Checkbox("Keep my listing alive", ref autoRefresh))
        {
            Config.AutoRefreshEnabled = autoRefresh;
            Config.Save();
            // Enabling it while a listing is already live: peek at the real time remaining and anchor to
            // it, without re-registering (so the listing's hour isn't reset). Gated on the recruiting
            // flag so enabling with no listing up never opens the PF.
            if (autoRefresh && Services.AutoRefreshService.IsRecruiting)
                plugin.AutoRefresh.StartRefresh(peek: true);
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
