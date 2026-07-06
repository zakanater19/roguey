using Content.Shared._Baseline.RogueTown;
using Content.Shared.Destructible;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownReplaceTileOnDestroyedSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownReplaceTileOnDestroyedComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnDestroyed(EntityUid uid, RogueTownReplaceTileOnDestroyedComponent component, DestructionEventArgs args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tileDef = _prototype.Index(component.Tile);
        var indices = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        _map.SetTile(gridUid, grid, indices, new Tile(tileDef.TileId));
    }
}
