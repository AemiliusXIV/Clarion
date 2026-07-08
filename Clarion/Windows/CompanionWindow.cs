using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Clarion.Windows;

/// <summary>
/// A small quick-apply pop-up that follows the in-game Recruitment Criteria form. Last-used preset
/// on a button, a dropdown for the rest, and a link into the full window. Apply-only - no renaming
/// or deleting here. Shown only while the criteria (setup) form is open and only when the full
/// window isn't already open; it never opens or closes the main window's own state.
/// </summary>
public sealed unsafe class CompanionWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private int selected;
    // This window's size last frame, so we can place its right edge at the form's left and (for the
    // centered mode) line up its middle with the form's middle.
    private float lastWidth = 260f;
    private float lastHeight = 120f;

    public CompanionWindow(Plugin plugin) : base("Clarion Quick##pfp_companion",
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing)
    {
        this.plugin = plugin;
        RespectCloseHotkey = false;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Sit just to the LEFT of the recruitment form (toward screen-middle, clear of the right side
        // some plugins use) and follow it wherever the player drags it.
        var addon = Plugin.GameGui.GetAddonByName<AtkUnitBase>("LookingForGroupCondition");
        if (addon == null || !addon->IsVisible)
            return;

        var x = addon->X - lastWidth - 6f;
        var y = (float)addon->Y;
        // Centered mode (default): line the pop-up's middle up with the form's middle, so a tall form
        // high on screen doesn't push the pop-up into the top-left corner.
        if (!plugin.Config.CompanionAnchorTopLeft && addon->RootNode != null)
        {
            var formHeight = addon->RootNode->Height * addon->Scale;
            y += Math.Max(0f, (formHeight - lastHeight) / 2f);
        }

        Position = new Vector2(x, y);
        PositionCondition = ImGuiCond.Always;
    }

    public override void Draw()
    {
        const float w = 260f;
        var presets = plugin.Config.Presets;

        ImGui.TextDisabled($"Clarion{Plugin.BuildTag} quick apply");
        ImGui.Spacing();

        if (presets.Count == 0)
        {
            ImGui.TextDisabled("No presets saved yet.");
            ImGui.Spacing();
            if (ImGui.Button("Open Clarion", new Vector2(w, 0)))
                OpenFull();
            CaptureSize();
            return;
        }

        // Last used: one big button that fills and recruits it straight away.
        var lastUsed = presets
            .Where(p => p.LastUsedAt != null)
            .OrderByDescending(p => p.LastUsedAt!.Value)
            .FirstOrDefault();
        if (lastUsed != null)
        {
            ImGui.TextDisabled("Last used");
            if (ImGui.Button(Shorten(lastUsed.Name), new Vector2(w, 0)))
                plugin.PresetLauncher.Launch(lastUsed, submit: true);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Fill and recruit with \"{lastUsed.Name}\" in one click.");
            ImGui.Spacing();
            ImGui.Spacing();
        }

        // Pick any preset, then fill (to review) or fill and recruit.
        ImGui.TextDisabled("Choose a preset");
        selected = Math.Clamp(selected, 0, presets.Count - 1);
        ImGui.SetNextItemWidth(w);
        if (ImGui.BeginCombo("##pfp_companion_pick", presets[selected].Name))
        {
            for (var i = 0; i < presets.Count; i++)
                if (ImGui.Selectable(presets[i].Name, i == selected))
                    selected = i;
            ImGui.EndCombo();
        }

        var half = (w - ImGui.GetStyle().ItemSpacing.X) / 2f;
        if (ImGui.Button("Fill", new Vector2(half, 0)))
            plugin.PresetLauncher.Launch(presets[selected], submit: false);
        ImGui.SameLine();
        if (ImGui.Button("Fill & recruit", new Vector2(half, 0)))
            plugin.PresetLauncher.Launch(presets[selected], submit: true);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("Open Clarion", new Vector2(w, 0)))
            OpenFull();

        CaptureSize();
    }

    // Remember our rendered size so next frame we can park the right edge at the form's left and,
    // in centered mode, line our middle up with the form's.
    private void CaptureSize()
    {
        var size = ImGui.GetWindowSize();
        lastWidth = size.X;
        lastHeight = size.Y;
    }

    // Hand off to the full window: open it, close ourselves. The main window then stays put on its
    // own (the form-close handler only ever closes this pop-up, never the main window).
    private void OpenFull()
    {
        plugin.MainWindow.IsOpen = true;
        IsOpen = false;
    }

    private static string Shorten(string s) => s.Length > 30 ? s[..30] + "..." : s;
}
