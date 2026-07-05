using System;
using System.Text;

namespace Clarion.Interop;

/// <summary>Helpers for the fixed-size UTF-8 byte spans FFXIVClientStructs generates for FixedSizeArray(isString: true) fields (e.g. RecruitmentSub.Comment).</summary>
public static class FixedSpanText
{
    public static string Read(Span<byte> span)
    {
        var terminator = span.IndexOf((byte)0);
        var length = terminator >= 0 ? terminator : span.Length;
        return Encoding.UTF8.GetString(span[..length]);
    }

    public static void Write(Span<byte> span, string value)
    {
        span.Clear();
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var length = Math.Min(bytes.Length, span.Length - 1); // leave room for null terminator
        bytes.AsSpan(0, length).CopyTo(span);
    }
}
