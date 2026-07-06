using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Clarion.Services;

namespace Clarion.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string newPresetName = string.Empty;

    // Inline rename state: the id of the preset being renamed, and its working name buffer.
    private Guid renamingId = Guid.Empty;
    private string renameBuffer = string.Empty;

    // Pending delete confirmation: the id awaiting a second click.
    private Guid pendingDeleteId = Guid.Empty;

    public MainWindow(Plugin plugin) : base("Clarion##pfp_main")
    {
        this.plugin = plugin;
        Size = new Vector2(520, 380);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(340, 240),
            MaximumSize = new Vector2(4000, 4000),
        };
    }

    public void Dispose() { }

    // "GreedOnly" -> "Greed Only" so enum names read as normal words in the UI.
    private static string Spaced(object value) =>
        System.Text.RegularExpressions.Regex.Replace(value.ToString() ?? string.Empty, "(?<=[a-z])(?=[A-Z])", " ");

    private void SaveCurrent()
    {
        if (!RecruitmentReader.IsCriteriaFormOpen())
        {
            Plugin.ChatGui.PrintError("[Clarion] Open the Recruitment Criteria form (Recruit Members) first so there's a configuration to save.");
            return;
        }

        var captured = RecruitmentReader.CaptureAsPreset("");
        if (captured == null)
        {
            Plugin.ChatGui.PrintError("[Clarion] Could not read the recruitment agent. Is the game running?");
            return;
        }

        // Name priority: typed name, else the duty's name, else a numbered fallback.
        var name = newPresetName.Trim();
        if (name.Length == 0)
            name = DutyNames.Get(captured.DutyId);
        if (name.Length == 0)
            name = $"Preset {plugin.Config.Presets.Count + 1}";
        captured.Name = name;

        plugin.Config.Presets.Add(captured);
        plugin.Config.Save();
        newPresetName = string.Empty;
    }

    private void ApplyPreset(Model.PartyFinderPreset preset, bool submit)
    {
        var result = RecruitmentApplier.Apply(preset, submit);
        if (result == RecruitmentApplier.ApplyResult.Success)
        {
            preset.LastUsedAt = DateTime.UtcNow;
            plugin.Config.Save();
            if (submit)
                plugin.AutoRefresh.NotifyListingPosted();
            Plugin.ChatGui.Print(submit
                ? $"[Clarion] Preset \"{preset.Name}\" applied, now recruiting."
                : $"[Clarion] Preset \"{preset.Name}\" filled in. Review it, then hit Recruit Members when ready.");
        }
        else if (result == RecruitmentApplier.ApplyResult.ConditionAddonNotOpen)
        {
            Plugin.ChatGui.PrintError("[Clarion] Open the recruitment form first: in Party Finder, click Recruit Members, then try again.");
        }
        else
        {
            Plugin.ChatGui.PrintError("[Clarion] Couldn't reach the Party Finder just now. Make sure you're logged in, then try again.");
        }
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Save the current Party Finder recruitment form as a preset, or apply a saved preset to the form.");
        ImGui.TextDisabled("Open the recruitment creation form in-game (Recruit Members) before saving or applying.");
        if (ImGui.Button("Settings"))
            plugin.OpenSettings();
        ImGui.Separator();

        ImGui.SetNextItemWidth(-130);
        ImGui.InputTextWithHint("##pfp_new_name", "New preset name (blank = duty name)", ref newPresetName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Save current", new Vector2(120, 0)))
            SaveCurrent();

        ImGui.Separator();

        if (ImGui.BeginChild("##pfp_preset_list", new Vector2(0, 0), true))
        {
            for (var i = 0; i < plugin.Config.Presets.Count; i++)
            {
                var preset = plugin.Config.Presets[i];
                ImGui.PushID(i);

                // Line 1: name + duty (or the rename editor).
                if (renamingId == preset.Id)
                {
                    ImGui.SetNextItemWidth(220);
                    var commit = ImGui.InputText("##pfp_rename", ref renameBuffer, 64, ImGuiInputTextFlags.EnterReturnsTrue);
                    ImGui.SameLine();
                    if (ImGui.Button("Save##rename", new Vector2(55, 0)) || commit)
                    {
                        var trimmed = renameBuffer.Trim();
                        if (trimmed.Length > 0)
                            preset.Name = trimmed;
                        plugin.Config.Save();
                        renamingId = Guid.Empty;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel##rename", new Vector2(60, 0)))
                        renamingId = Guid.Empty;
                }
                else
                {
                    ImGui.TextUnformatted(preset.Name);
                    ImGui.SameLine();
                    // Secondary line: the duty (unless the name already is the duty) and the loot rule,
                    // labelled so "Normal" reads as a loot rule rather than a mystery word.
                    var dutyName = DutyNames.Get(preset.DutyId);
                    string dutyPart;
                    if (dutyName.Length == 0)
                        dutyPart = "unknown duty, ";
                    else if (string.Equals(dutyName, preset.Name, StringComparison.OrdinalIgnoreCase))
                        dutyPart = "";
                    else
                        dutyPart = $"{dutyName}, ";
                    ImGui.TextDisabled($"({dutyPart}Loot: {Spaced(preset.LootRule)})");
                }

                // Line 2: actions, so nothing clips no matter how long the name is.
                if (ImGui.Button("Fill only", new Vector2(80, 0)))
                    ApplyPreset(preset, submit: false);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Load this preset into the open recruitment form so you can check it before recruiting.");

                ImGui.SameLine();
                if (ImGui.Button("Fill & recruit", new Vector2(110, 0)))
                    ApplyPreset(preset, submit: true);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Load this preset and start recruiting in one step.");

                ImGui.SameLine();
                if (ImGui.Button("Rename", new Vector2(70, 0)))
                {
                    renamingId = preset.Id;
                    renameBuffer = preset.Name;
                    pendingDeleteId = Guid.Empty;
                }

                ImGui.SameLine();
                if (pendingDeleteId == preset.Id)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                    if (ImGui.Button("Confirm delete", new Vector2(110, 0)))
                    {
                        plugin.Config.Presets.RemoveAt(i);
                        plugin.Config.Save();
                        pendingDeleteId = Guid.Empty;
                        ImGui.PopStyleColor();
                        ImGui.PopID();
                        break;
                    }
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    if (ImGui.Button("Keep", new Vector2(50, 0)))
                        pendingDeleteId = Guid.Empty;
                }
                else if (ImGui.Button("Delete", new Vector2(70, 0)))
                {
                    pendingDeleteId = preset.Id;
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }
}
