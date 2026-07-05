using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Clarion.Interop;
using Clarion.Model;

namespace Clarion.Services;

public static unsafe class RecruitmentReader
{
    /// <summary>True when the Recruitment Criteria form is open, i.e. the struct reflects what's on screen.</summary>
    public static bool IsCriteriaFormOpen()
    {
        var addon = Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("LookingForGroupCondition");
        return addon != null && addon->IsVisible;
    }

    /// <summary>Reads the live StoredRecruitmentInfo into a new preset, e.g. from a form the player just filled out by hand.</summary>
    public static PartyFinderPreset? CaptureAsPreset(string name)
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
            return null;

        ref var info = ref agent->StoredRecruitmentInfo;

        var preset = new PartyFinderPreset
        {
            Name = name,
            Category = info.SelectedCategory,
            DutyId = info.SelectedDutyId,
            Objective = info.Objective,
            BeginnerFriendly = info.BeginnerFriendly != 0,
            CompletionStatus = info.CompletionStatus,
            DutyFinderSettingFlags = info.DutyFinderSettingFlags,
            LootRule = info.LootRule,
            LanguageFlags = info.LanguageFlags,
            NumberOfSlotsInMainParty = info.NumberOfSlotsInMainParty,
            LimitRecruitingToWorld = info.LimitRecruitingToWorld != 0,
            OnePlayerPerJob = info.OnePlayerPerJob != 0,
            NumberOfGroups = info.NumberOfGroups,
            Comment = FixedSpanText.Read(info.Comment),
        };

        var slotFlags = info.SlotFlags;
        for (var i = 0; i < slotFlags.Length; i++)
            preset.SlotFlags.Add(slotFlags[i]);

        return preset;
    }

#if PFP_DEVTOOLS
    public static void DebugDump()
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
        {
            Plugin.Log.Information("DebugDump: agent is null");
            return;
        }

        // Recruiting-state signals: which of these is reliable when the PF browser is CLOSED tells
        // us what to use to detect an active listing in the background.
        var browserOpen = Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("LookingForGroup") != null;
        Plugin.Log.Information($"  RECRUIT STATE: OwnListingId={agent->OwnListingId} IsAgentActive={agent->IsAgentActive()} browserAddonPresent={browserOpen}");

        // Candidate persistent "am I recruiting" signals, readable with every window closed. If
        // UsingPartyFinder is true only while a listing is live (and false while merely browsing),
        // it replaces the whole belief model. Check this line in each state: no PF, browsing PF,
        // recruiting (window open), recruiting (windows closed), just after ending the listing.
        Plugin.Log.Information($"  COND FLAGS: UsingPartyFinder={Plugin.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.UsingPartyFinder]} " +
            $"RecruitingWorldOnly={Plugin.Condition[(Dalamud.Game.ClientState.Conditions.ConditionFlag)98]}");

        ref var info = ref agent->StoredRecruitmentInfo;
        var comment = FixedSpanText.Read(info.Comment);
        Plugin.Log.Information($"Category={info.SelectedCategory} Duty={info.SelectedDutyId} Objective={info.Objective} " +
            $"Loot={info.LootRule} OnePerJob={info.OnePlayerPerJob} Lang={info.LanguageFlags} Slots={info.NumberOfSlotsInMainParty} " +
            $"Groups={info.NumberOfGroups} DFSettings={info.DutyFinderSettingFlags} Comment=\"{comment}\"");

        var slotFlags = info.SlotFlags;
        for (var i = 0; i < 8 && i < slotFlags.Length; i++)
            Plugin.Log.Information($"  Slot[{i}] = 0x{slotFlags[i]:X}");

        // Duty-selection candidates - the displayed duty is driven by one of these, NOT by
        // StoredRecruitmentInfo.SelectedDutyId (which our writes update but the form ignores).
        // Raw scalar reads only: do NOT call GetName()/any virtual here - it does an Excel lookup
        // that dereferences null when the content id is empty and crashes the game process (a
        // native access violation that C# try/catch cannot intercept). We resolve names offline.
        Plugin.Log.Information($"  ContentRoulette.RowId={agent->ContentRoulette.ContentRouletteRowId}");
        Plugin.Log.Information($"  PartyContent.CfcRowId={agent->PartyContent.ContentFinderConditionRowId} PartyContentId={agent->PartyContent.PartyContentId}");

        // PF-internal content descriptor (offset 0x60 on the agent). Candidate for the real
        // displayed duty, kept inside Party Finder rather than the separate Duty Finder system.
        Plugin.Log.Information($"  ContentUI.LookupInfo: type={agent->ContentUI.LookupInfo.ContentType} id={agent->ContentUI.LookupInfo.Id}");

        // The live-listing view (LookingForGroupDetail) shows "Time Limit: N minutes remaining".
        // If we can read that text node, we can sync the auto-refresh timer to the real remaining time.
        var detailBase = Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>("LookingForGroupDetail");
        if (detailBase != null)
        {
            var detail = (FFXIVClientStructs.FFXIV.Client.UI.AddonLookingForGroupDetail*)detailBase;
            var node = detail->TimeRemainingTextNode;
            Plugin.Log.Information($"  Detail.TimeRemaining=\"{(node != null ? node->NodeText.ToString() : "<null node>")}\"");
        }
        else
        {
            Plugin.Log.Information("  Detail: not open");
        }

        // Raw bytes of the RecruitmentSub header, to catch any duty-related field near
        // SelectedDutyId (0x10) that isn't named in FFXIVClientStructs.
        var basePtr = (byte*)System.Runtime.CompilerServices.Unsafe.AsPointer(ref info);
        var sb = new System.Text.StringBuilder("  RecruitmentSub[0x00..0x30]:");
        for (var i = 0; i < 0x30; i++)
            sb.Append(i % 8 == 0 ? $" |{i:X2}| {basePtr[i]:X2}" : $" {basePtr[i]:X2}");
        Plugin.Log.Information(sb.ToString());

        // The duty-picker list selection lives in the LookingForGroupCondition addon itself.
        // Reading its AtkValues is a plain memory read (no Excel lookups), safe to log.
        DumpAddonValues("LookingForGroupCondition");
    }

    private static void DumpAddonValues(string addonName)
    {
        var addon = Plugin.GameGui.GetAddonByName<FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase>(addonName);
        if (addon == null || !addon->IsVisible)
        {
            Plugin.Log.Information($"  Addon {addonName}: not open");
            return;
        }

        var count = addon->AtkValuesCount;
        Plugin.Log.Information($"  Addon {addonName}: {count} AtkValues");
        for (var i = 0; i < count; i++)
        {
            var desc = Interop.AtkValueLogging.Describe(&addon->AtkValues[i]);
            // Only log ints/uints/bools and short strings - skip anything that isn't a plausible
            // duty id/index/name to keep the log readable while diffing two states.
            Plugin.Log.Information($"    [{i}] {desc}");
        }
    }

    /// <summary>Lists every currently-visible UI addon by name, so we can identify the duty-picker addon.</summary>
    public static void DumpVisibleAddons()
    {
        var mgr = FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance();
        if (mgr == null)
        {
            Plugin.Log.Information("DumpVisibleAddons: manager null");
            return;
        }

        ref var list = ref mgr->AtkUnitManager.AllLoadedUnitsList;
        Plugin.Log.Information($"Visible addons ({list.Count} loaded):");
        for (var i = 0; i < list.Count; i++)
        {
            var unit = list.Entries[i].Value;
            if (unit == null || !unit->IsVisible)
                continue;
            Plugin.Log.Information($"  {unit->NameString}");
        }
    }
#endif
}
