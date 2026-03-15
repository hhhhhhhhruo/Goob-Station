// SPDX-FileCopyrightText: 2026 OpenAI
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.Disease;
using Content.Goobstation.Shared.Disease.Components;
using Content.Pirate.Server.StationEvents.Components;
using Content.Server.Chat.Managers;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.Chat;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Pirate.Server.StationEvents.Events;

public sealed class RandomVirusRule : StationEventSystem<RandomVirusRuleComponent>
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override void Started(EntityUid uid, RandomVirusRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        var candidates = new List<Entity<DiseaseCarrierComponent>>();
        var query = EntityQueryEnumerator<ActorComponent, HumanoidAppearanceComponent, MobStateComponent, DiseaseCarrierComponent, TransformComponent>();
        while (query.MoveNext(out var target, out _, out _, out var mobState, out var carrier, out var xform))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            if (_station.GetOwningStation(target, xform) != chosenStation)
                continue;

            if (_disease.HasAnyDisease((target, carrier)))
                continue;

            candidates.Add((target, carrier));
        }

        if (candidates.Count == 0)
            return;

        var targetCount = component.TargetCount;
        if (candidates.Count < component.TargetCount)
        {
            Log.Warning($"RandomVirus event found only {candidates.Count} valid targets, requested {component.TargetCount}.");
            targetCount = candidates.Count;
        }

        var pickedTargets = RobustRandom.GetItems(candidates, targetCount, allowDuplicates: false);
        foreach (var target in pickedTargets)
        {
            InfectTarget(target, component);
        }
    }

    private void InfectTarget(Entity<DiseaseCarrierComponent> target, RandomVirusRuleComponent component)
    {
        var pandemic = component.PandemicChance > 0f && RobustRandom.Prob(component.PandemicChance);
        var diseaseBase = pandemic ? component.PandemicDiseaseBase : component.DiseaseBase;
        var diseaseComplexity = pandemic ? component.PandemicDiseaseComplexity : component.DiseaseComplexity;
        var possibleTypes = pandemic ? component.PandemicPossibleTypes : component.PossibleTypes;

        var disease = _disease.MakeRandomDisease(diseaseBase, diseaseComplexity);
        if (disease == null)
            return;

        if (!TryComp<DiseaseComponent>(disease.Value, out var diseaseComp))
        {
            QueueDel(disease.Value);
            return;
        }

        if (possibleTypes.Count > 0)
        {
            diseaseComp.DiseaseType = RobustRandom.Pick(possibleTypes);
            Dirty(disease.Value, diseaseComp);
        }

        if (!_disease.TryInfect(target, disease.Value))
        {
            QueueDel(disease.Value);
            return;
        }

        if (component.Message == null || !_player.TryGetSessionByEntity(target.Owner, out var session))
            return;

        var message = Loc.GetString("chat-manager-server-wrap-message", ("message", Loc.GetString(component.Message)));
        _chat.ChatMessageToOne(ChatChannel.Local, message, message, EntityUid.Invalid, false, session.Channel);
    }
}
