using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Clarion.Model;

namespace Clarion.Services;

/// <summary>
/// One click from nothing to a posted party. If the Recruitment Criteria form is already open, it
/// just applies the preset. Otherwise it opens the Party Finder browser, fires Recruit Members to
/// bring up a fresh form, waits for it, then applies (and recruits, for Fill and recruit). Every
/// wait runs on the framework tick, and every step is guarded so a hiccup aborts with a message
/// instead of wedging.
/// </summary>
public sealed unsafe class PresetLauncher
{
    private readonly Plugin plugin;
    private bool running;

    public PresetLauncher(Plugin plugin) => this.plugin = plugin;

    public void Launch(PartyFinderPreset preset, bool submit)
    {
        // Form already up: apply immediately, nothing to open.
        if (RecruitmentReader.IsCriteriaFormOpen())
        {
            Apply(preset, submit);
            return;
        }

        if (running)
            return;
        running = true;
        Guard(() => StepEnsureBrowser(preset, submit, 0));
    }

    private void StepEnsureBrowser(PartyFinderPreset preset, bool submit, int attempt)
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
        {
            Fail("agent unavailable");
            return;
        }

        if (IsVisible("LookingForGroup"))
        {
            // Let the browser settle a moment, then bring up a fresh criteria form.
            Plugin.Framework.RunOnTick(() => Guard(() =>
            {
                if (!plugin.RecruitmentOps.OpenCriteriaForm())
                {
                    Fail("could not open the recruitment form");
                    return;
                }
                StepWaitForm(preset, submit, 0);
            }), delayTicks: 10);
            return;
        }

        if (attempt == 0)
            agent->Show();

        if (attempt < 40)
            Defer(() => StepEnsureBrowser(preset, submit, attempt + 1));
        else
            Fail("Party Finder did not open");
    }

    private void StepWaitForm(PartyFinderPreset preset, bool submit, int attempt)
    {
        if (IsVisible("LookingForGroupCondition"))
        {
            // Let the freshly-opened form finish initialising before we apply. Fill-only then does a
            // cancel-and-reopen to display the struct-written fields, which needs a settled form.
            Plugin.Framework.RunOnTick(() => Guard(() => Apply(preset, submit)), delayTicks: 20);
            return;
        }

        if (attempt < 40)
            Defer(() => StepWaitForm(preset, submit, attempt + 1));
        else
            Fail("recruitment form did not open");
    }

    private void Apply(PartyFinderPreset preset, bool submit)
    {
        running = false;
        var result = RecruitmentApplier.Apply(preset, submit);
        if (result == RecruitmentApplier.ApplyResult.Success)
        {
            preset.LastUsedAt = DateTime.UtcNow;
            plugin.Config.Save();
            if (submit)
                plugin.AutoRefresh.NotifyListingPosted();
            Plugin.ChatGui.Print(submit
                ? $"[Clarion] Preset \"{preset.Name}\" applied, now recruiting."
                : $"[Clarion] Preset \"{preset.Name}\" filled in. Review it, then hit Recruit Members when ready.");
        }
        else
        {
            Plugin.ChatGui.PrintError("[Clarion] Couldn't apply the preset just now. Open Party Finder and Recruit Members, then try again.");
        }
    }

    private void Fail(string reason)
    {
        running = false;
        DevLog.Info($"[PFP launch] aborted: {reason}");
        Plugin.ChatGui.PrintError("[Clarion] Couldn't open the recruitment form automatically. Open Party Finder, click Recruit Members, then apply.");
    }

    private void Guard(Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Fail($"exception: {ex.Message}");
        }
    }

    private void Defer(Action step) => Plugin.Framework.RunOnTick(() => Guard(step), delayTicks: 5);

    private static bool IsVisible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(name);
        return addon != null && addon->IsVisible;
    }
}
