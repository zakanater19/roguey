using System.Numerics;
using Content.Shared._Baseline.RogueTown;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Baseline.RogueTown;

/// <summary>
/// Keeps a south-facing sconce hidden when viewed through the wall from its north side.
/// Its entity remains on the southern floor tile while its sprite is offset onto the wall tile.
/// </summary>
public sealed class RogueTownSouthWallTorchVisibilitySystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly PointLightSystem _light = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eye = _eye.CurrentEye.Position;
        var query = EntityQueryEnumerator<RogueTownTorchHolderComponent, SpriteComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var holder, out var sprite, out var xform))
        {
            if (!holder.SouthFacing)
                continue;

            var visible = eye.MapId == xform.MapID && IsEyeSouthOfWall(eye.Position, xform);
            _sprite.SetVisible((uid, sprite), visible);
            _light.SetContainerOccluded(uid, !visible);
        }
    }

    private bool IsEyeSouthOfWall(Vector2 eyePosition, TransformComponent xform)
    {
        var torchPosition = _transform.GetWorldPosition(xform);

        if (xform.GridUid is { } grid)
        {
            var inverseGrid = _transform.GetInvWorldMatrix(grid);
            eyePosition = Vector2.Transform(eyePosition, inverseGrid);
            torchPosition = Vector2.Transform(torchPosition, inverseGrid);
        }

        // The entity is centered in the southern tile; its north edge is the wall boundary.
        return eyePosition.Y < torchPosition.Y + 0.5f;
    }
}
