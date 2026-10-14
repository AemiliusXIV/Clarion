#if PFP_DEVTOOLS
using System;
using System.Collections.Generic;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Interop;

/// <summary>
/// Dev-only reverse-engineering tool: hooks the function the recruitment UI uses to push field
/// changes, so a human can observe which AtkValue arrays each control sends.
///
/// CRITICAL: this hook sits directly on the recruitment change/submit path. While it is enabled,
/// the game's own "Recruit Members" / "Cancel" actions route through our managed detour, and any
/// mismatch (calling convention, return value) makes the game report "Failed to change/cancel
/// party recruitment" and locks the window. For that reason the hook is DISABLED by default and
/// only turned on for the duration of an explicit capture session. Do not submit a real listing
/// while capture is active.
/// </summary>
public sealed unsafe class RecruitmentCaptureHook : IDisposable
{
    public readonly struct CapturedCall
    {
        public DateTime Time { get; init; }
        public string[] Values { get; init; }
    }

    private readonly List<CapturedCall> captures = new();
    public IReadOnlyList<CapturedCall> Captures => captures;
    public bool SignaturesResolved { get; private set; }

    private bool isCapturing;

    /// <summary>
    /// Enables the hook only while true. Setting this is the ONLY thing that puts the detour on
    /// the recruitment path; the plugin loads with it off so normal recruitment is never touched.
    /// </summary>
    public bool IsCapturing
    {
        get => isCapturing;
        set
        {
            if (isCapturing == value)
                return;
            isCapturing = value;

            if (updateValuesHook == null)
                return;

            if (value)
            {
                updateValuesHook.Enable();
                Plugin.Log.Warning("[PFP] Capture hook ENABLED. Do not submit a real Party Finder listing while capturing.");
            }
            else
            {
                updateValuesHook.Disable();
                Plugin.Log.Information("[PFP] Capture hook disabled.");
            }
        }
    }

    private delegate void RCUpdateValuesDelegate(AgentLookingForGroup* agent, AtkValue* values);

    [Signature("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 48 8B D9 48 89 4D ?? 48 8B CA 48 8B F2",
        DetourName = nameof(OnUpdateValues))]
    private readonly Hook<RCUpdateValuesDelegate>? updateValuesHook;

    public RecruitmentCaptureHook()
    {
        Plugin.GameInterop.InitializeFromAttributes(this);

        SignaturesResolved = updateValuesHook != null;
        if (!SignaturesResolved)
            Plugin.Log.Warning("RecruitmentCaptureHook: signature not found for this game version. Capture unavailable.");

        // Deliberately NOT enabled here. Enable happens only via IsCapturing = true.
    }

    private void OnUpdateValues(AgentLookingForGroup* agent, AtkValue* values)
    {
        try
        {
            if (isCapturing && values != null)
            {
                const int maxRead = 3;
                var described = new string[maxRead];
                for (var i = 0; i < maxRead; i++)
                    described[i] = AtkValueLogging.Describe(values + i);

                captures.Add(new CapturedCall { Time = DateTime.Now, Values = described });
                Plugin.Log.Information($"[PFP capture] {string.Join(" | ", described)}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "RecruitmentCaptureHook.OnUpdateValues threw");
        }

        updateValuesHook!.Original(agent, values);
    }

    public void ClearCaptures() => captures.Clear();

    public void Dispose()
    {
        // Turn the hook off before disposing so nothing is left patched.
        if (updateValuesHook != null && updateValuesHook.IsEnabled)
            updateValuesHook.Disable();
        updateValuesHook?.Dispose();
    }
}
#endif
