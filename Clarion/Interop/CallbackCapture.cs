#if PFP_DEVTOOLS
using System;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Interop;

/// <summary>
/// Dev-only: hooks AtkUnitBase.FireCallback and logs the multi-value callbacks the PF addons send
/// (e.g. what the main browser fires when Recruit Members is clicked), so we can replay them.
/// Opt-in; disabled by default. Log-only, calls Original unchanged.
/// </summary>
public sealed unsafe class CallbackCapture : IDisposable
{
    private Hook<AtkUnitBase.Delegates.FireCallback>? hook;
    private bool capturing;

    public bool IsCapturing
    {
        get => capturing;
        set
        {
            if (capturing == value)
                return;
            capturing = value;

            if (value)
            {
                hook ??= Plugin.GameInterop.HookFromAddress<AtkUnitBase.Delegates.FireCallback>(
                    AtkUnitBase.MemberFunctionPointers.FireCallback, Detour);
                hook.Enable();
            }
            else
            {
                hook?.Disable();
            }

            Plugin.Log.Information($"[PFP] Callback capture {(value ? "ON - click a PF button; its callback is logged" : "OFF")}");
        }
    }

    private bool Detour(AtkUnitBase* thisPtr, uint valueCount, AtkValue* values, bool updateState)
    {
        try
        {
            if (capturing && thisPtr != null)
            {
                var name = thisPtr->NameString;
                if (name is "LookingForGroup" or "LookingForGroupCondition" or "LookingForGroupDetail")
                {
                    var n = (int)Math.Min(valueCount, 8u);
                    var parts = new string[n];
                    for (var i = 0; i < n; i++)
                        parts[i] = AtkValueLogging.Describe(values + i);
                    Plugin.Log.Information($"[PFP cb] {name} count={valueCount}: {string.Join(" | ", parts)}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "CallbackCapture detour threw");
        }

        return hook!.Original(thisPtr, valueCount, values, updateState);
    }

    public void Dispose()
    {
        if (hook != null && hook.IsEnabled)
            hook.Disable();
        hook?.Dispose();
    }
}
#endif
