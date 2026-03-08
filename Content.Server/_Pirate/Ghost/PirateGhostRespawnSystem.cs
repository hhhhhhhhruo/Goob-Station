using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Server.GameTicking;
using Content.Shared._Pirate.Ghost;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Ghost;

public sealed class PirateGhostRespawnSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly Dictionary<NetUserId, GhostRespawnState> _states = new();
    private readonly Dictionary<NetUserId, PendingGhostTransition> _pendingTransitions = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<MindContainerComponent, MindRemovedMessage>(OnMindRemoved);
        SubscribeLocalEvent<MindContainerComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<GhostComponent, PlayerAttachedEvent>(OnGhostPlayerAttached);

        SubscribeNetworkEvent<GhostRespawnLobbyRequest>(OnGhostRespawnLobbyRequest);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        // PIRATE: GameTicker player spawns start a fresh crew cycle, including no-lobby fallback respawns.
        _states[ev.Player.UserId] = new GhostRespawnState
        {
            HasCrewCycle = true,
            TimerArmed = false,
            RespawnAvailableAt = TimeSpan.Zero
        };

        _pendingTransitions.Remove(ev.Player.UserId);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _states.Clear();
        _pendingTransitions.Clear();
    }

    private void OnMindRemoved(Entity<MindContainerComponent> ent, ref MindRemovedMessage args)
    {
        if (args.Mind.Comp.UserId is not { } userId)
            return;

        if (!TryGetState(userId, out var state) || state == null || !state.HasCrewCycle || state.TimerArmed)
            return;

        _pendingTransitions[userId] = new PendingGhostTransition
        {
            Immediate = HasComp<CryostorageContainedComponent>(ent.Owner)
        };
    }

    private void OnMindAdded(Entity<MindContainerComponent> ent, ref MindAddedMessage args)
    {
        if (args.Mind.Comp.UserId is not { } userId)
            return;

        if (HasComp<GhostComponent>(ent.Owner))
        {
            ArmTimerIfNeeded(userId);
            SendStatusToUser(userId);
            return;
        }

        _pendingTransitions.Remove(userId);
    }

    private void OnGhostPlayerAttached(Entity<GhostComponent> ent, ref PlayerAttachedEvent args)
    {
        ArmTimerIfNeeded(args.Player.UserId);
        SendStatus(args.Player);
    }

    private void OnGhostRespawnLobbyRequest(GhostRespawnLobbyRequest ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session.AttachedEntity is not { Valid: true } attached || !HasComp<GhostComponent>(attached))
        {
            Log.Warning($"User {session.Name} sent an invalid {nameof(GhostRespawnLobbyRequest)}");
            return;
        }

        ArmTimerIfNeeded(session.UserId);
        var status = GetStatus(session.UserId);
        if (!status.CanRespawn)
        {
            SendStatus(session);
            return;
        }

        ClearState(session.UserId);
        _gameTicker.Respawn(session);
    }

    public PirateGhostRespawnDebugState GetDebugState(NetUserId userId)
    {
        if (!TryGetState(userId, out var state) || state == null)
            return default;

        return new PirateGhostRespawnDebugState(state.HasCrewCycle, state.TimerArmed, state.RespawnAvailableAt);
    }

    public PirateGhostRespawnAvailability GetDebugAvailability(NetUserId userId)
    {
        var status = GetStatus(userId);
        return new PirateGhostRespawnAvailability(status.CanRespawn, status.RemainingTime);
    }

    private bool TryGetState(NetUserId userId, [NotNullWhen(true)] out GhostRespawnState? state)
    {
        if (_states.TryGetValue(userId, out var found))
        {
            state = found;
            return true;
        }

        state = null;
        return false;
    }

    private void ArmTimerIfNeeded(NetUserId userId)
    {
        if (!TryGetState(userId, out var state) || state == null || !state.HasCrewCycle)
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        if (state.TimerArmed)
        {
            _pendingTransitions.Remove(userId);
            return;
        }

        var availableAt = _timing.CurTime + GetRespawnDelay();
        if (_pendingTransitions.Remove(userId, out var pending) && pending != null && pending.Immediate)
            availableAt = _timing.CurTime;

        state.TimerArmed = true;
        state.RespawnAvailableAt = availableAt;
    }

    private GhostRespawnStatus GetStatus(NetUserId userId)
    {
        if (!TryGetState(userId, out var state) || state == null || !state.HasCrewCycle)
            return new GhostRespawnStatus(true, TimeSpan.Zero);

        if (!state.TimerArmed)
            return new GhostRespawnStatus(false, GetRespawnDelay());

        var remainingTime = state.RespawnAvailableAt - _timing.CurTime;
        if (remainingTime <= TimeSpan.Zero)
            return new GhostRespawnStatus(true, TimeSpan.Zero);

        return new GhostRespawnStatus(false, remainingTime);
    }

    private TimeSpan GetRespawnDelay()
    {
        var delay = _cfg.GetCVar(CCVars.GhostRespawnDelay);
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    private void SendStatusToUser(NetUserId userId)
    {
        if (!_player.TryGetSessionById(userId, out var session))
            return;

        SendStatus(session);
    }

    private void SendStatus(ICommonSession session)
    {
        var status = GetStatus(session.UserId);
        RaiseNetworkEvent(new GhostRespawnStatusEvent(status.CanRespawn, status.RemainingTime), session.Channel);
    }

    private void ClearState(NetUserId userId)
    {
        _states.Remove(userId);
        _pendingTransitions.Remove(userId);
    }

    private sealed class GhostRespawnState
    {
        public bool HasCrewCycle;
        public bool TimerArmed;
        public TimeSpan RespawnAvailableAt;
    }

    private sealed class PendingGhostTransition
    {
        public bool Immediate;
    }

    private readonly record struct GhostRespawnStatus(bool CanRespawn, TimeSpan RemainingTime);
}

public readonly record struct PirateGhostRespawnDebugState(bool HasCrewCycle, bool TimerArmed, TimeSpan RespawnAvailableAt);

public readonly record struct PirateGhostRespawnAvailability(bool CanRespawn, TimeSpan RemainingTime);
