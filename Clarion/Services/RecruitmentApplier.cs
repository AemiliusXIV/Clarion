using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Clarion.Interop;
using Clarion.Model;

namespace Clarion.Services;

/// <summary>
/// Writes a preset directly into AgentLookingForGroup's StoredRecruitmentInfo, then submits it.
/// Submit mechanism (Callback.Fire(addon, true, 0) on LookingForGroupCondition = "Recruit Members")
/// is the same one PartyFinderRefresh already uses in production for re-confirming an edit, so
/// it's proven, not guessed.
///
/// This does NOT open the creation form itself - the LookingForGroupCondition addon must already
/// be open (i.e. the player clicked "Recruit Members" from the main Party Finder window to get a
/// blank form up) before calling Apply. Finding the trigger for opening a *fresh* form
/// programmatically is a follow-up; for now "one manual click to open the form, one plugin click
/// to fill and submit it" is still a large reduction from filling every field by hand.
/// </summary>
public static class RecruitmentApplier
{
    public enum ApplyResult
    {
        Success,
        ConditionAddonNotOpen,
        AgentUnavailable,
    }

    public static unsafe ApplyResult Apply(PartyFinderPreset preset, bool submit)
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
        {
            DevLog.Info("[PFP apply] agent unavailable");
            return ApplyResult.AgentUnavailable;
        }

        var conditionAddon = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupCondition");
        if (conditionAddon == null || !conditionAddon->IsVisible)
        {
            DevLog.Info("[PFP apply] LookingForGroupCondition addon not open/visible");
            return ApplyResult.ConditionAddonNotOpen;
        }

        // Duty first, via the form's own operations (category op 12 -> duty scan op 13). These
        // update live and reset the role slots, so they MUST run before the struct writes below,
        // or op 13's reset would wipe the roles we set.
        if (preset.Category != AgentLookingForGroup.DutyCategory.None)
        {
            var ops = Plugin.Instance.RecruitmentOps;
            ops.SelectCategory((uint)preset.Category);
            if (!ops.SelectDutyById(preset.DutyId))
                DevLog.Info($"[PFP apply] duty {preset.DutyId} not found in category {preset.Category}");
        }

        WriteToAgent(agent, preset);

        if (submit)
        {
            DevLog.Info("[PFP apply] firing submit callback (0) on LookingForGroupCondition");
            Callback.Fire(conditionAddon, true, 0);
        }
        else
        {
            // Fill-only: reopen the form so the struct-written fields (roles, comment, objective,
            // loot, language, one-per-job) display. Duty/category are already live via ops.
            DevLog.Info("[PFP apply] fields written (fill-only); reopening form to refresh display");
            Plugin.Instance.RecruitmentOps.ReopenCriteriaForm();
        }

        return ApplyResult.Success;
    }

    private static unsafe void WriteToAgent(AgentLookingForGroup* agent, PartyFinderPreset preset)
    {
        ref var info = ref agent->StoredRecruitmentInfo;

        DevLog.Info($"[PFP apply] writing preset \"{preset.Name}\": Category={preset.Category} Duty={preset.DutyId} " +
            $"Objective={preset.Objective} Loot={preset.LootRule} OnePerJob={preset.OnePlayerPerJob} Lang={preset.LanguageFlags} " +
            $"Slots={preset.NumberOfSlotsInMainParty} Groups={preset.NumberOfGroups} Comment=\"{preset.Comment}\"");

        info.SelectedCategory = preset.Category;
        info.SelectedDutyId = preset.DutyId;
        info.Objective = preset.Objective;
        info.BeginnerFriendly = (byte)(preset.BeginnerFriendly ? 1 : 0);
        info.CompletionStatus = preset.CompletionStatus;
        info.DutyFinderSettingFlags = preset.DutyFinderSettingFlags;
        info.LootRule = preset.LootRule;
        info.LanguageFlags = preset.LanguageFlags;
        info.NumberOfSlotsInMainParty = preset.NumberOfSlotsInMainParty;
        info.LimitRecruitingToWorld = (byte)(preset.LimitRecruitingToWorld ? 1 : 0);
        info.OnePlayerPerJob = (byte)(preset.OnePlayerPerJob ? 1 : 0);
        info.NumberOfGroups = preset.NumberOfGroups;

        var slotFlags = info.SlotFlags;
        for (var i = 0; i < slotFlags.Length && i < preset.SlotFlags.Count; i++)
            slotFlags[i] = preset.SlotFlags[i];

        FixedSpanText.Write(info.Comment, preset.Comment);
    }
}
