// SPDX-FileCopyrightText: 2026 OpenAI
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Disease;
using Content.Pirate.Server.StationEvents.Events;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(RandomVirusRule))]
public sealed partial class RandomVirusRuleComponent : Component
{
    /// <summary>
    /// How many unique targets to infect when the event fires.
    /// </summary>
    [DataField]
    public int TargetCount = 2;

    /// <summary>
    /// Disease prototype to mutate from.
    /// </summary>
    [DataField]
    public EntProtoId DiseaseBase = "DiseaseBase";

    /// <summary>
    /// Complexity used when generating a random disease.
    /// </summary>
    [DataField]
    public float DiseaseComplexity = 20f;

    /// <summary>
    /// If provided, random diseases will be forced to one of these types.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseaseTypePrototype>> PossibleTypes = new() { "Viral" };

    /// <summary>
    /// Chance in the range [0.0, 1.0] that an infection rolls the stronger pandemic profile instead.
    /// </summary>
    [DataField]
    public float PandemicChance = 0f;

    /// <summary>
    /// Pandemic profile disease prototype. Mirrors the plague mouse admeme profile by default.
    /// </summary>
    [DataField]
    public EntProtoId PandemicDiseaseBase = "DiseaseBaseMouse";

    /// <summary>
    /// Pandemic profile complexity. Mirrors MobMousePandemic.
    /// </summary>
    [DataField]
    public float PandemicDiseaseComplexity = 100f;

    /// <summary>
    /// Allowed disease types for the pandemic profile.
    /// </summary>
    [DataField]
    public List<ProtoId<DiseaseTypePrototype>> PandemicPossibleTypes = new() { "Bacterial", "Viral" };

    /// <summary>
    /// Private message sent to infected players.
    /// </summary>
    [DataField]
    public LocId? Message = "station-event-random-virus-message";
}
