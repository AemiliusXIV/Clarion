using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Services;

/// <summary>
/// Keeps an active Party Finder listing alive by re-confirming it near the end of its hour.
/// A listing lasts 60 minutes; refreshing (Edit -> Recruit on your own listing) resets that timer.
///
/// The refresh drives the PF UI through callbacks, which the game does NOT treat as input, so it
/// leaves the player's away/AFK state untouched - confirmed in-game (afkTimer/onlineStatus unchanged
/// across a refresh). So there's nothing to preserve. If a future patch ever makes a refresh clear
/// away and we want to hold it, do NOT poke the InputTimerModule floats directly (that desyncs the
/// away status from the disconnect timer); drive it through real input the way NightmareXIV's
/// AntiAfkKick does (SendMessage WM_KEYDOWN/UP of LControl to the FFXIVGAME window). Idle fields for
/// reference: AfkTimer @ 0x10, ContentInputTimer @ 0x14, InputTimer @ 0x18 on InputTimerModule.
/// </summary>
public sealed unsafe class AutoRefreshService : IDisposable
{
    private readonly Plugin plugin;
    private Configuration Config => plugin.Config;

    // A Party Finder listing lasts this long; used to convert "N minutes remaining" into an anchor.
    private const int ListingMinutes = 60;

    // Non-null while we're tracking a live listing's hour; null means "not recruiting". The
    // UsingPartyFinder condition flag is the source of truth for whether a listing exists (readable
    // with every window closed, false while merely browsing), so this is just the anchor for timing.
    private DateTime? countingSinceUtc;
    private bool refreshing;
    private bool disposed;
    private bool wasBrowserOpenAtStart;
    private bool peekOnly; // this run just reads the remaining time and anchors, without re-registering

    // Short cooldown set after an aborted attempt so we don't retry every frame. Deliberately not a
    // full interval: near expiry that would miss the listing entirely. Independent of countingSinceUtc
    // so the live-time sync can't override it.
    private DateTime nextAllowedRefreshUtc = DateTime.MinValue;

    public AutoRefreshService(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        disposed = true;
        Plugin.Framework.Update -= OnUpdate;
    }

    /// <summary>True while the player has an active Party Finder recruitment, readable in the background regardless of window state.</summary>
    public static bool IsRecruiting => Plugin.Condition[ConditionFlag.UsingPartyFinder];

    /// <summary>Called after the plugin posts a listing (Fill + submit). The UsingPartyFinder flag anchors the
    /// clock on its own a frame or two later; this just surfaces the first-time notice immediately.</summary>
    public void NotifyListingPosted() => ShowNoticeOnce();

    private void OnUpdate(IFramework framework)
    {
        if (!Config.Enabled || !Config.AutoRefreshEnabled || refreshing)
            return;

        // UsingPartyFinder is the whole detection model: true iff a listing of ours is live, readable
        // with every window closed, and false while merely browsing others' listings. countingSinceUtc
        // tracks its hour.
        if (!IsRecruiting)
        {
            if (countingSinceUtc != null)
            {
                countingSinceUtc = null; // listing ended (cancelled or expired) - stop tracking
                DevLog.Info("[PFP refresh] recruitment ended (flag cleared); stopping");
            }
            return;
        }

        if (countingSinceUtc == null)
        {
            // Flag just went true (posted, edited, or the plugin was loaded/enabled mid-listing).
            countingSinceUtc = DateTime.UtcNow;
            ShowNoticeOnce();
            DevLog.Info("[PFP refresh] recruitment detected; countdown anchored");
        }

        // Exact anchor whenever the live-listing view is open: "N minutes remaining" pins the clock to
        // the real time left, correcting the approximate anchor above (and a manual edit that reset it).
        var remaining = ReadTimeRemainingMinutes();
        if (remaining >= 0)
            countingSinceUtc = DateTime.UtcNow - TimeSpan.FromMinutes(Math.Clamp(ListingMinutes - remaining, 0, ListingMinutes));

        // Fire once the listing is down to the configured minutes-remaining. Anchor + this interval =
        // the moment "minutes remaining" hits the threshold.
        var remainingThreshold = Math.Clamp(Config.RefreshWhenMinutesRemaining, 1, ListingMinutes - 1);
        var interval = ListingMinutes - remainingThreshold;
        if ((DateTime.UtcNow - countingSinceUtc.Value).TotalMinutes < interval)
            return;

        if (!IsSafeToRefresh())
            return; // try again next tick once out of combat / a cutscene / etc.

        if (DateTime.UtcNow < nextAllowedRefreshUtc)
            return; // brief cooldown after a failed attempt

        DevLog.Info("[PFP refresh] countdown elapsed; starting refresh");
        StartRefresh();
    }

    private void ShowNoticeOnce()
    {
        if (Config.AutoRefreshNoticeShown)
            return;
        Config.AutoRefreshNoticeShown = true;
        Config.Save();
        Plugin.ChatGui.Print("[Clarion] Auto-refresh is on and will keep this Party Finder listing alive. " +
            "For accurate timing, keep Clarion running from before you post a listing. Turn this off in /clarion cfg.");
    }


    // Reads minutes remaining from the live-listing view's "N minutes remaining" text, or -1 if that
    // view isn't open/readable. Only the leading digits are parsed, so it's language-independent.
    private static int ReadTimeRemainingMinutes()
    {
        var detailBase = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupDetail");
        if (detailBase == null)
            return -1;

        var node = ((AddonLookingForGroupDetail*)detailBase)->TimeRemainingTextNode;
        if (node == null)
            return -1;

        var text = node->NodeText.ToString();
        var value = 0;
        var found = false;
        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                value = value * 10 + (c - '0');
                found = true;
            }
            else if (found)
            {
                break; // stop at the first non-digit after the number
            }
        }

        // Sanity: the value should fall within a listing's lifetime; ignore anything odd (e.g. a
        // seconds display near expiry, which the plugin normally never reaches).
        return found && value >= 0 && value <= ListingMinutes ? value : -1;
    }

    private bool IsSafeToRefresh()
    {
        // Don't start a refresh while the recruitment criteria form is open: the player is editing or
        // ending the listing by hand, and driving Edit/Recruit underneath them fights their input.
        if (IsAddonVisible("LookingForGroupCondition"))
            return false;

        return !Plugin.Condition[ConditionFlag.InCombat]
            && !Plugin.Condition[ConditionFlag.BetweenAreas]
            && !Plugin.Condition[ConditionFlag.OccupiedInCutSceneEvent]
            && !Plugin.Condition[ConditionFlag.WatchingCutscene];
    }

    /// <summary>
    /// Runs the refresh sequence now, regardless of the timer. Returns false if it can't start.
    /// The listing is verified after the browser opens (OwnListingId isn't readable while closed),
    /// so there's no pre-check here.
    /// </summary>
    public bool StartRefresh(bool peek = false)
    {
        if (refreshing)
            return false;

        var agent = AgentLookingForGroup.Instance();
        if (agent == null)
            return false;

        refreshing = true;
        peekOnly = peek;
        wasBrowserOpenAtStart = IsAddonVisible("LookingForGroup");

        DevLog.Info(peek ? "[PFP refresh] peek starting" : "[PFP refresh] starting");
        Guard(() => StepEnsureBrowser(0));
        return true;
    }

    // Every step runs through Guard so an exception anywhere aborts cleanly (clears the refreshing
    // flag, closes the browser) instead of leaving the sequence wedged. Defer schedules the next step.
    private void Guard(Action step)
    {
        if (disposed)
            return;
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Abort($"exception: {ex.Message}");
        }
    }

    private void Defer(Action step) => Plugin.Framework.RunOnTick(() => Guard(step), delayTicks: 5);

    // 1. Make sure the main PF browser is open, then open our own listing directly. OpenListingByContentId
    // returns false when there's no listing to open (it ended while we weren't looking) - that's how we
    // detect "gone". OwnListingId can't be polled here: it only populates once the detail view opens,
    // which is the very step we're about to trigger.
    private void StepEnsureBrowser(int attempt)
    {
        var agent = AgentLookingForGroup.Instance();
        var playerState = PlayerState.Instance();
        if (agent == null || playerState == null)
        {
            Abort("agent or player state null");
            return;
        }

        if (IsAddonVisible("LookingForGroup"))
        {
            if (!agent->OpenListingByContentId(playerState->ContentId))
            {
                Abort("no active listing (OpenListingByContentId returned false)");
                return;
            }
            StepWait("LookingForGroupDetail", 0, OnDetailOpen);
            return;
        }

        if (attempt == 0)
            agent->Show();

        if (attempt < 40)
            Defer(() => StepEnsureBrowser(attempt + 1));
        else
            Abort("browser did not open");
    }

    private void OnDetailOpen(AtkUnitBase* detail)
    {
        if (peekOnly)
        {
            // Just learn the real time left and anchor to it - no Edit/Recruit, so the listing's hour
            // isn't reset. Used when auto-refresh is switched on mid-listing.
            PeekReadTime(0);
            return;
        }

        Callback.Fire(detail, true, 0); // Edit
        StepWait("LookingForGroupCondition", 0, OnConditionOpen);
    }

    private void PeekReadTime(int attempt)
    {
        var remaining = ReadTimeRemainingMinutes();
        if (remaining >= 0)
        {
            countingSinceUtc = DateTime.UtcNow - TimeSpan.FromMinutes(Math.Clamp(ListingMinutes - remaining, 0, ListingMinutes));
            DevLog.Info($"[PFP refresh] peek synced to {remaining} min remaining");
            refreshing = false;
            Cleanup();
            return;
        }

        // The time text can take a frame or two to populate after the view opens.
        if (attempt < 20)
        {
            Defer(() => PeekReadTime(attempt + 1));
            return;
        }

        countingSinceUtc ??= DateTime.UtcNow; // give up on exact time; track from now
        DevLog.Info("[PFP refresh] peek could not read time; anchored to now");
        refreshing = false;
        Cleanup();
    }

    private void OnConditionOpen(AtkUnitBase* condition)
    {
        // The edit form needs time to repopulate its current values before it will accept a
        // re-registration; firing Recruit immediately yields "Unable to complete party recruitment
        // registration" and the edit is cancelled (timer not reset). Wait, then fire Recruit.
        Plugin.Framework.RunOnTick(() => Guard(FireRecruit), delayTicks: 60);
    }

    private void FireRecruit()
    {
        var condition = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupCondition");
        if (condition == null || !condition->IsVisible)
        {
            Abort("condition closed before recruit");
            return;
        }

        Callback.Fire(condition, true, 0); // Recruit Members - re-confirms, resets the timer
        StepWaitClosed("LookingForGroupCondition", 0, Finish);
    }

    // Poll until an addon is visible, then invoke onOpen with it.
    private void StepWait(string addonName, int attempt, AddonAction onOpen)
    {
        Defer(() =>
        {
            var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(addonName);
            if (addon != null && addon->IsVisible)
            {
                onOpen(addon);
                return;
            }
            if (attempt < 40)
                StepWait(addonName, attempt + 1, onOpen);
            else
                Abort($"{addonName} did not open");
        });
    }

    // Poll until an addon is gone/hidden, then run onClosed.
    private void StepWaitClosed(string addonName, int attempt, Action onClosed)
    {
        Defer(() =>
        {
            var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(addonName);
            if (addon == null || !addon->IsVisible)
            {
                onClosed();
                return;
            }
            if (attempt < 40)
                StepWaitClosed(addonName, attempt + 1, onClosed);
            else
                Abort($"{addonName} did not close");
        });
    }

    private void Finish()
    {
        // A successful refresh just reset the listing to a full hour, so re-anchor the clock to now.
        countingSinceUtc = DateTime.UtcNow;
        refreshing = false;
        DevLog.Info("[PFP refresh] done");
#if PFP_DEVTOOLS
        if (Config.VerboseLogging)
            Plugin.ChatGui.Print("[Clarion] Party Finder listing refreshed.");
#endif
        Cleanup();
    }

    private void Abort(string reason)
    {
        refreshing = false; // reset first so a throw in cleanup can't wedge the sequence

        // Hold off ~1 minute before retrying so a failed attempt doesn't re-fire every frame, then let
        // it try again (a listing near expiry still gets several more chances). The flag stops retries
        // entirely once the listing actually ends.
        nextAllowedRefreshUtc = DateTime.UtcNow.AddMinutes(1);

        DevLog.Info($"[PFP refresh] aborted: {reason}");
        Cleanup();
    }

    // Leave things as we found them: close the browser if we opened it.
    private void Cleanup()
    {
        try
        {
            if (!wasBrowserOpenAtStart)
            {
                var agent = AgentLookingForGroup.Instance();
                if (agent != null && IsAddonVisible("LookingForGroup"))
                    agent->Hide();
            }
        }
        catch (Exception ex)
        {
            DevLog.Info($"[PFP refresh] cleanup threw: {ex.Message}");
        }
    }

    private static bool IsAddonVisible(string name)
    {
        var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>(name);
        return addon != null && addon->IsVisible;
    }

    private delegate void AddonAction(AtkUnitBase* addon);
}
