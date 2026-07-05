#if PFP_DEVTOOLS
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Interop;

/// <summary>
/// Best-effort stringification of an AtkValue for diagnostic logging. This only needs to be
/// readable during manual field-mapping, not exhaustively correct for every AtkValueType.
/// </summary>
public static unsafe class AtkValueLogging
{
    public static string Describe(AtkValue* value)
    {
        if (value == null)
            return "<null>";

        try
        {
            return value->Type switch
            {
                AtkValueType.Int => $"Int={value->Int}",
                AtkValueType.UInt => $"UInt={value->UInt}",
                AtkValueType.Bool => $"Bool={value->Byte != 0}",
                AtkValueType.Float => $"Float={value->Float}",
                AtkValueType.String or AtkValueType.ConstString =>
                    $"String=\"{(value->String.Value != null ? value->String.ToString() : string.Empty)}\"",
                _ => $"{value->Type} (raw uint={value->UInt})",
            };
        }
        catch
        {
            return $"{value->Type} (unreadable)";
        }
    }
}
#endif
