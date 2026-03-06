// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Sprite;
using Content.Client.Stealth;
using Content.Client.Viewport;
using Content.Shared.Mobs.Components;
using Content.Shared.Stealth.Components;
using Robust.Client.ComponentTrees;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using System.Numerics;

namespace Content.Client._Pirate.Photo;

/// <summary>
/// Captures mobs that are visible inside the camera viewport bounds.
/// </summary>
public sealed class PhotoCaptureEntityDetectorSystem : EntitySystem
{
    private const int MaxEntitiesHardLimit = 64;

    [Dependency] private readonly OccluderSystem _occluderSystem = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    [Dependency] private readonly StealthSystem _stealthSystem = default!;
    private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    private EntityQuery<FadingSpriteComponent> _fadingQuery = default!;
    private EntityQuery<StealthComponent> _stealthQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _fadingQuery = GetEntityQuery<FadingSpriteComponent>();
        _stealthQuery = GetEntityQuery<StealthComponent>();
    }

    public List<NetEntity> CaptureVisibleEntities(ScalingViewport viewport, int maxEntities = 256)
    {
        const float MinVisibleFraction = 0.05f;

        var eye = viewport.Eye;
        if (eye == null)
            return new List<NetEntity>();

        if (maxEntities <= 0)
            return new List<NetEntity>();

        if (eye.Position.MapId == MapId.Nullspace)
            return new List<NetEntity>();

        var viewportSize = viewport.ViewportSize;
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            return new List<NetEntity>();

        if (!TryGetViewportWorldBounds(viewport, out var worldBounds))
            return new List<NetEntity>();

        var effectiveCapacity = System.Math.Min(maxEntities, MaxEntitiesHardLimit);
        var visibleSprites = _spriteTree.QueryAabb(eye.Position.MapId, worldBounds);
        var unique = new HashSet<NetEntity>(effectiveCapacity);
        var eyeWorldPosition = eye.Position.Position + eye.Offset;

        foreach (var sprite in visibleSprites)
        {
            if (!sprite.Component.Visible)
                continue;

            var entity = sprite.Uid;
            if (!_mobStateQuery.HasComp(entity))
                continue;

            var fadingVisibleFraction = 1f;
            if (_fadingQuery.HasComp(entity))
                fadingVisibleFraction = System.Math.Clamp(sprite.Component.Color.A, 0f, 1f);

            var stealthVisibleFraction = 1f;
            if (_stealthQuery.TryGetComponent(entity, out var stealth))
                stealthVisibleFraction = System.Math.Clamp(_stealthSystem.GetVisibility(entity, stealth), 0f, 1f);

            var visibleFraction = fadingVisibleFraction * stealthVisibleFraction;
            if (visibleFraction < MinVisibleFraction)
                continue;

            if (IsOccluded(eye.Position.MapId, eyeWorldPosition, sprite.Uid, sprite.Transform.WorldPosition))
                continue;

            if (!EntityManager.TryGetNetEntity(entity, out var net) || net == null)
                continue;

            unique.Add(net.Value);
            if (unique.Count >= effectiveCapacity)
                return new List<NetEntity>(unique);
        }

        return new List<NetEntity>(unique);
    }

    private bool IsOccluded(MapId mapId, Vector2 eyeWorldPosition, EntityUid entity, Vector2 targetWorldPosition)
    {
        var toTarget = targetWorldPosition - eyeWorldPosition;
        var distance = toTarget.Length();
        if (distance <= 0.001f)
            return false;

        var ray = new Ray(eyeWorldPosition, toTarget / distance);
        var hits = _occluderSystem.IntersectRayWithPredicate(
            mapId,
            ray,
            distance,
            entity,
            static (uid, targetEntity) => uid == targetEntity,
            returnOnFirstHit: true);

        return hits.Count > 0;
    }

    private static bool TryGetViewportWorldBounds(ScalingViewport viewport, out Box2Rotated bounds)
    {
        var localToScreen = viewport.GetLocalToScreenMatrix();
        var viewportLocalSize = (Vector2) (viewport.ViewportSize * viewport.CurrentRenderScale);
        var screenTopLeft = Vector2.Transform(Vector2.Zero, localToScreen);
        var screenBottomRight = Vector2.Transform(viewportLocalSize, localToScreen);
        var screenBounds = new Box2(Vector2.Min(screenTopLeft, screenBottomRight), Vector2.Max(screenTopLeft, screenBottomRight));
        var screenBox = new Box2Rotated(screenBounds, Angle.Zero);

        if (!Matrix3x2.Invert(viewport.GetWorldToScreenMatrix(), out var screenToWorld))
        {
            bounds = default;
            return false;
        }

        bounds = screenToWorld.TransformBounds(screenBox);
        return true;
    }
}
