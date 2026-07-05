using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Clarion.Model;

[Serializable]
public class PartyFinderPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Preset";

    public AgentLookingForGroup.DutyCategory Category { get; set; }
    public ushort DutyId { get; set; }
    public AgentLookingForGroup.Objective Objective { get; set; } = AgentLookingForGroup.Objective.DutyCompletion;
    public bool BeginnerFriendly { get; set; }
    public AgentLookingForGroup.CompletionStatus CompletionStatus { get; set; }
    public AgentLookingForGroup.DutyFinderSetting DutyFinderSettingFlags { get; set; }
    public AgentLookingForGroup.LootRule LootRule { get; set; }
    public AgentLookingForGroup.Language LanguageFlags { get; set; }
    public byte NumberOfSlotsInMainParty { get; set; } = 8;
    public bool LimitRecruitingToWorld { get; set; }
    public bool OnePlayerPerJob { get; set; }
    public byte NumberOfGroups { get; set; } = 1;
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Job-restriction bitmask per party slot, one entry per slot of the game's 48-slot array
    /// (index 0-7 = main party for Normal). Populated verbatim from the live struct by
    /// RecruitmentReader; must NOT be pre-seeded, or the capture is offset and apply writes zeros
    /// into the real slots (which reads as "no restrictions / open party").
    /// </summary>
    public List<ulong> SlotFlags { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
