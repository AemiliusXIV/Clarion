#if PFP_DEVTOOLS
using System;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Clarion.Interop;

namespace Clarion.Windows;

/// <summary>
/// Phase 1 field-mapping tool. Start capture, toggle exactly one control in the real Party
/// Finder recruitment window, label the entry that appears, repeat. Export when done.
/// </summary>
public sealed class DiagnosticWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string pendingLabel = string.Empty;

    public DiagnosticWindow(Plugin plugin) : base("Clarion Diagnostic##pfp_diag")
    {
        this.plugin = plugin;
        Size = new Vector2(640, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 260),
            MaximumSize = new Vector2(4000, 4000),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var hook = plugin.CaptureHook;

        if (!hook.SignaturesResolved)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f),
                "Capture signature not found for this game version. Diagnostic capture is unavailable.");
            return;
        }

        var capturing = hook.IsCapturing;
        if (ImGui.Checkbox("Capturing", ref capturing))
            hook.IsCapturing = capturing;

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
            hook.ClearCaptures();

        ImGui.SameLine();
        if (ImGui.Button("Export to config"))
        {
            foreach (var c in hook.Captures)
            {
                var entry = new CapturedEntry
                {
                    CapturedAt = c.Time,
                    Label = "(unlabeled)",
                    AddonName = "LookingForGroupCondition",
                };
                for (var i = 0; i < c.Values.Length; i++)
                    entry.Values.Add(new CapturedValue { Index = i, Type = "raw", Value = c.Values[i] });
                plugin.Config.DiagnosticCaptures.Add(entry);
            }
            plugin.Config.Save();
        }

        ImGui.TextWrapped("Toggle one control at a time in the real Party Finder creation window, then type what you just did below and press Label on the newest capture.");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##pfp_label", "Label for the most recent capture (e.g. 'toggled Tank slot 1 off')", ref pendingLabel, 128);

        if (ImGui.Button("Label most recent") && hook.Captures.Count > 0)
        {
            var last = hook.Captures[^1];
            plugin.Config.DiagnosticCaptures.Add(new CapturedEntry
            {
                CapturedAt = last.Time,
                Label = pendingLabel,
                AddonName = "LookingForGroupCondition",
            });
            plugin.Config.Save();
            pendingLabel = string.Empty;
        }

        ImGui.Separator();
        ImGui.Text($"Captured this session: {hook.Captures.Count}");
        ImGui.Text($"Saved to config: {plugin.Config.DiagnosticCaptures.Count}");

        if (ImGui.BeginChild("##pfp_capture_list", new Vector2(0, 0), true))
        {
            for (var i = hook.Captures.Count - 1; i >= 0; i--)
            {
                var c = hook.Captures[i];
                ImGui.TextUnformatted($"{c.Time:HH:mm:ss.fff}  {string.Join(" | ", c.Values)}");
            }
        }
        ImGui.EndChild();
    }
}
#endif
