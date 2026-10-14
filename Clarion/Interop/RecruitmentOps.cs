using ECommons.Automation;
using ECommons.Automation.UIInput;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Interop;

/// <summary>
/// Release-safe invoker for the recruitment form's internal field-update function (the one the
/// UI itself calls when you change a field). We only ever CALL it - the same thing the game does
/// when you click - never hook it, so it carries none of the risk the capture hook did.
///
/// Operation ids observed by capture:
///   op 13, [1]=index  -> select the duty at list index within the current category
///   op 11, [1]=6, [2]=value -> average item level (from Krepyn's stub)
/// </summary>
public sealed unsafe class RecruitmentOps
{
    private delegate void RCUpdateValuesDelegate(AgentLookingForGroup* agent, AtkValue* values);

    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B D9 48 89 4D ?? 48 8B CA 48 8B F2")]
    private readonly RCUpdateValuesDelegate? updateValues;

    public bool Available => updateValues != null;

    // Callback value the main PF browser fires to open the Recruitment Criteria form (captured from
    // a real Recruit Members click: LookingForGroup count=1, Int=14). A plain button click doesn't
    // route through the agent; this callback does.
    private const int RecruitMembersCallbackValue = 14;

    public RecruitmentOps()
    {
        Plugin.GameInterop.InitializeFromAttributes(this);
        if (updateValues == null)
            Plugin.Log.Warning("RecruitmentOps: update-values signature not found; duty select unavailable.");
    }

    /// <summary>
    /// Closes and reopens the Recruitment Criteria form so it re-reads the struct into every field.
    /// Struct writes don't refresh an open form, so this is how the non-op fields (roles, comment,
    /// objective, loot, language, one-per-job) become visible after apply.
    ///
    /// Cancel closes the criteria (settings are preserved); once it's closed we fire the captured
    /// "open criteria" callback (value 14) on the still-open main browser to reopen it fresh.
    /// </summary>
    /// <summary>
    /// Fires the "Recruit Members" callback on the already-open main PF browser to bring up a fresh
    /// Recruitment Criteria form. Returns false if the browser isn't open. Used by the one-click
    /// "post from nothing" flow, which opens the browser first.
    /// </summary>
    public bool OpenCriteriaForm()
    {
        var mainBase = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroup");
        if (mainBase == null || !mainBase->IsVisible)
            return false;

        Callback.Fire(mainBase, true, RecruitMembersCallbackValue);
        DevLog.Info("[PFP launch] fired Recruit Members callback to open the form");
        return true;
    }

    public bool ReopenCriteriaForm()
    {
        var conditionBase = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupCondition");
        if (conditionBase != null && conditionBase->IsVisible)
        {
            var condition = (AddonLookingForGroupCondition*)conditionBase;
            if (condition->CancelButton != null)
                condition->CancelButton->ClickAddonButton(conditionBase);
        }

        ScheduleReopen(0);
        return true;
    }

    private void ScheduleReopen(int attempt)
    {
        Plugin.Framework.RunOnTick(() =>
        {
            // The main PF browser stays open on its own; we do NOT touch it. Fire the recruit-open
            // callback on it once the criteria has finished closing.
            var conditionStillOpen = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupCondition");
            if (conditionStillOpen != null && conditionStillOpen->IsVisible)
            {
                if (attempt < 20)
                    ScheduleReopen(attempt + 1);
                return;
            }

            var mainBase = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroup");
            if (mainBase != null && mainBase->IsVisible)
            {
                // Recruit Members is handled by the agent; a plain button click doesn't carry the
                // right callback value. Fire the callback the game sends for it (value captured).
                Callback.Fire((AtkUnitBase*)mainBase, true, RecruitMembersCallbackValue);
                DevLog.Info("[PFP reopen] fired Recruit Members callback on the PF browser");
                return;
            }

            if (attempt < 20)
                ScheduleReopen(attempt + 1);
            else
                DevLog.Info("[PFP reopen] gave up waiting for the PF browser");
        }, delayTicks: 10);
    }

    /// <summary>
    /// Selects a category by its DutyCategory flag (op 12). The dropdown list index equals the
    /// flag's bit position, since the enum is defined as 1&lt;&lt;index, so we derive it directly -
    /// no per-server table needed. None (0) maps to index 0.
    /// </summary>
    public bool SelectCategory(uint categoryFlag)
    {
        var index = categoryFlag == 0 ? 0u : (uint)System.Numerics.BitOperations.Log2(categoryFlag);
        return SelectCategoryByIndex(index);
    }

    /// <summary>Selects the category at the given dropdown list index (op 12).</summary>
    public bool SelectCategoryByIndex(uint index)
    {
        var agent = AgentLookingForGroup.Instance();
        if (updateValues == null || agent == null)
            return false;

        FireOp(agent, 12, index);
        return true;
    }

    /// <summary>
    /// Selects the duty with the given id by scanning list indices and stopping when the live
    /// ContentUI id matches. op 13 updates ContentUI in memory instantly, so this is fast and needs
    /// no reopen. Survives the list order/size changing across patches - we match by id, not index.
    /// The current category must already contain the duty (set the category first).
    /// </summary>
    public bool SelectDutyById(uint dutyId, uint maxScan = 64)
    {
        var agent = AgentLookingForGroup.Instance();
        if (updateValues == null || agent == null)
            return false;

        uint prev = uint.MaxValue;
        var sameCount = 0;
        for (uint i = 0; i < maxScan; i++)
        {
            FireOp(agent, 13, i);
            var cur = agent->ContentUI.LookupInfo.Id;
            if (cur == dutyId)
                return true;

            // Firing an index past the end of the list stops changing ContentUI; use that to bail.
            if (cur == prev)
            {
                if (++sameCount >= 2)
                    break;
            }
            else
            {
                sameCount = 0;
            }
            prev = cur;
        }
        return false;
    }

    private void FireOp(AgentLookingForGroup* agent, int op, uint value)
    {
        var values = stackalloc AtkValue[3];
        for (var i = 0; i < 3; i++)
        {
            values[i].Type = 0;
            values[i].Int = 0;
        }
        values[0].Type = AtkValueType.Int;
        values[0].Int = op;
        values[1].Type = AtkValueType.UInt;
        values[1].UInt = value;

        updateValues!(agent, values);
    }
}
