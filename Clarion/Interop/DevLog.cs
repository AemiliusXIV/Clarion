using System.Diagnostics;

namespace Clarion;

/// <summary>
/// All diagnostic logging goes through here. The methods are marked [Conditional("PFP_DEVTOOLS")],
/// so in a normal (non-dev) build the compiler strips every call site entirely - including the
/// argument/string construction. A shipped release therefore contains no logging code at all,
/// not merely disabled logging. There is nothing an end-user can toggle to bring it back.
/// </summary>
public static class DevLog
{
    [Conditional("PFP_DEVTOOLS")]
    public static void Info(string message)
    {
#if PFP_DEVTOOLS
        // Belt and braces: [Conditional] already strips every call site in a release build, but this
        // also empties the method body so the release DLL carries no logging code path at all.
        if (Plugin.Instance.Config.VerboseLogging)
            Plugin.Log.Information(message);
#endif
    }
}
