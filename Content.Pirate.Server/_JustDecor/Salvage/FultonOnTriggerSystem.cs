using System;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Salvage.Fulton;
using Robust.Shared.Timing;
using Content.Shared.Popups;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Pirate.Shared._JustDecor.Salvage.Fulton;

namespace Content.Pirate.Server._JustDecor.Salvage.Fulton;

public sealed class FultonOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FultonOnTriggerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FultonOnTriggerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FultonOnTriggerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, FultonOnTriggerComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || args.Handled || !args.CanReach)
            return;

        var target = args.Target.Value;

        // Try to link to a beacon
        if (HasComp<FultonBeaconComponent>(target))
        {
            component.Beacon = target;
            _popup.PopupEntity(Loc.GetString("fulton-on-trigger-linked"), target, args.User);
            args.Handled = true;
            return;
        }

        if (!HasComp<MobStateComponent>(target))
            return;

        if (component.Beacon == null)
        {
            _popup.PopupEntity(Loc.GetString("fulton-on-trigger-no-beacon"), target, args.User);
            return;
        }

        if (HasComp<FultonOnTriggerComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("fulton-fultoned"), target, args.User);
            return;
        }

        var newComp = AddComp<FultonOnTriggerComponent>(target);
        newComp.TargetState = component.TargetState;
        newComp.FultonDuration = component.FultonDuration;
        newComp.Beacon = component.Beacon;
        newComp.Removeable = component.Removeable;
        newComp.Sound = component.Sound;
        newComp.Enabled = true;

        _popup.PopupEntity(Loc.GetString("fulton-on-trigger-applied"), target, args.User);

        // Handle stack consumption
        if (TryComp<StackComponent>(uid, out var stack))
        {
            _stack.Use(uid, 1, stack);
        }
        else
        {
            QueueDel(uid);
        }

        args.Handled = true;
    }

    private void OnExamined(EntityUid uid, FultonOnTriggerComponent component, ExaminedEvent args)
    {
        if (!component.Enabled)
            return;

        args.PushMarkup(Loc.GetString("fulton-on-trigger-examine", ("state", component.TargetState.ToString())));
    }

    private void OnMobStateChanged(EntityUid uid, FultonOnTriggerComponent component, MobStateChangedEvent args)
    {
        if (!component.Enabled || args.NewMobState != component.TargetState)
            return;

        if (HasComp<FultonedComponent>(uid))
            return;

        var beacon = component.Beacon;

        // If no beacon on our component, check if there's a FultonComponent we can borrow from
        if (beacon == null && TryComp<FultonComponent>(uid, out var fulton))
        {
            beacon = fulton.Beacon;
        }

        if (beacon == null || Deleted(beacon))
            return;

        var fultoned = AddComp<FultonedComponent>(uid);
        fultoned.Beacon = beacon;
        fultoned.FultonDuration = component.FultonDuration;
        fultoned.NextFulton = _timing.CurTime + component.FultonDuration;
        fultoned.Removeable = component.Removeable;
        fultoned.Sound = component.Sound;

        _popup.PopupEntity(Loc.GetString("fulton-on-trigger-popup"), uid, uid);

        // Remove the trigger component after use (one-shot)
        RemComp<FultonOnTriggerComponent>(uid);
    }
}
