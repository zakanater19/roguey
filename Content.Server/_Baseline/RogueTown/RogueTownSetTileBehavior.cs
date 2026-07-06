using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Baseline.RogueTown;

[Serializable]
[DataDefinition]
public sealed partial class RogueTownSetTileBehavior : IThresholdBehavior
{
    [DataField("tile", required: true)]
    public ProtoId<ContentTileDefinition> Tile { get; set; }

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        var entityManager = system.EntityManager;

        if (!entityManager.TryGetComponent<TransformComponent>(owner, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var map = entityManager.System<SharedMapSystem>();
        var tileDef = system.PrototypeManager.Index(Tile);
        var indices = map.LocalToTile(gridUid, grid, xform.Coordinates);

        map.SetTile(gridUid, grid, indices, new Tile(tileDef.TileId));
    }
}
