using Content.Server.Radio.EntitySystems;
using Content.Server.Pinpointer;
using Content.Shared.Mobs.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Server.Station.Systems; // DOWNSTREAM-TPirates: death rattle update
using Robust.Shared.Map; // DOWNSTREAM-TPirates: death rattle update

namespace Content.Server.Trigger.Systems;

public sealed class RattleOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!; // DOWNSTREAM-TPirates: death rattle update
    [Dependency] private readonly StationSystem _station = default!; // DOWNSTREAM-TPirates: death rattle update

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RattleOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<RattleOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        var target = ent.Comp.TargetUser ? args.User : ent.Owner;

        if (target == null)
            return;

        if (!TryComp<MobStateComponent>(target.Value, out var mobstate))
            return;

        args.Handled = true;

        if (!ent.Comp.Messages.TryGetValue(mobstate.CurrentState, out var messageId))
            return;

        // Gets the location of the user
        var posText = GetPositionText(target.Value, ent.Comp.ReportCoordinates); // DOWNSTREAM-TPirates: death rattle update

        var message = Loc.GetString(messageId, ("user", target.Value), ("position", posText));
        // Sends a message to the radio channel specified by the implant
        _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(ent.Comp.RadioChannel), ent.Owner);
        #region DOWNSTREAM-TPirates: death rattle update
        if (!ent.Comp.RelayToStationMedicalWhenOffStation || _station.GetOwningStation(target.Value) != null)
            return;

        if (TryGetRelaySourceGrid(out var relaySource))
            _radio.SendRadioMessage(ent.Owner, message, _prototypeManager.Index(ent.Comp.OffStationRelayChannel), relaySource);
        #endregion
    }
    #region DOWNSTREAM-TPirates: death rattle update
    private string GetPositionText(EntityUid target, bool reportCoordinates)
    {
        var location = CapitalizeFirst(
            FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(target)),
            Loc.GetString("rattle-on-trigger-unknown-position"));
        if (!reportCoordinates)
            return location;

        var pos = _transform.GetMapCoordinates(target);
        if (pos.MapId == MapId.Nullspace)
            return location;

        var x = (int)pos.Position.X;
        var y = (int)pos.Position.Y;
        return $"{location} ({x}, {y})";
    }

    private static string CapitalizeFirst(string text, string fallback)
    {
        if (string.IsNullOrEmpty(text))
            return fallback;

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private bool TryGetRelaySourceGrid(out EntityUid relaySource)
    {
        foreach (var station in _station.GetStations())
        {
            if (_station.GetLargestGrid(station) is { } grid)
            {
                relaySource = grid;
                return true;
            }
        }

        relaySource = EntityUid.Invalid;
        return false;
    }
    #endregion
}
