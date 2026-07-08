using System;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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

    // Hover overview: a plain-language summary of exactly what this preset would write to the form.
    private static string BuildOverview(Model.PartyFinderPreset preset)
    {
        var sb = new StringBuilder();
        var duty = DutyNames.Get(preset.DutyId);
        sb.AppendLine($"Duty: {(duty.Length > 0 ? duty : preset.Category != AgentLookingForGroup.DutyCategory.None ? Spaced(preset.Category) : "None")}");
        if (preset.Objective != AgentLookingForGroup.Objective.None)
            sb.AppendLine($"Objective: {Spaced(preset.Objective)}");
        sb.AppendLine($"Loot: {Spaced(preset.LootRule)}");
        if (preset.LanguageFlags != default)
            sb.AppendLine($"Languages: {preset.LanguageFlags}");

        var party = $"Slots: {preset.NumberOfSlotsInMainParty}";
        if (preset.NumberOfGroups > 1)
            party += $" x {preset.NumberOfGroups} groups";
        if (preset.OnePlayerPerJob)
            party += ", one job per player";
        sb.AppendLine(party);

        if (preset.CompletionStatus != AgentLookingForGroup.CompletionStatus.None)
            sb.AppendLine($"Completion: {Spaced(preset.CompletionStatus)}");
        if (preset.BeginnerFriendly)
            sb.AppendLine("Beginner friendly");
        if (!string.IsNullOrWhiteSpace(preset.Comment))
            sb.AppendLine($"Comment: {preset.Comment}");

        return sb.ToString().TrimEnd();
    }

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

    // The launcher opens Party Finder and the recruitment form itself if they aren't open, then
    // applies (and recruits, when submit is set). If the form is already open it applies straight away.
    private void ApplyPreset(Model.PartyFinderPreset preset, bool submit)
        => plugin.PresetLauncher.Launch(preset, submit);

    public override void Draw()
    {
        ImGui.TextWrapped("Save the current Party Finder recruitment form as a preset, or apply a saved preset in one click.");
        ImGui.TextDisabled("To save, open the recruitment form in-game first (Recruit Members). Applying a preset opens it for you.");
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

                    // Deliberate hover target so the overview only shows when you point at it, not
                    // whenever the cursor passes over the row.
                    ImGui.SameLine();
                    ImGui.TextDisabled("(?)");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(BuildOverview(preset));
                }

                // Line 2: actions, so nothing clips no matter how long the name is.
                if (ImGui.Button("Fill only", new Vector2(80, 0)))
                    ApplyPreset(preset, submit: false);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Open the recruitment form if needed and fill in this preset, so you can review it before recruiting.");

                ImGui.SameLine();
                if (ImGui.Button("Fill & recruit", new Vector2(110, 0)))
                    ApplyPreset(preset, submit: true);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Open Party Finder if needed, fill in this preset, and start recruiting in one step.");

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
