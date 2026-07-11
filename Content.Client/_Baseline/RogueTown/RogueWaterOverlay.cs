using System.Numerics;
using System.Linq;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Baseline.RogueTown;

/// <summary>
/// Draws the animated RogueTown water surface over the otherwise-static tile atlas.
/// </summary>
public sealed class RogueWaterOverlay : Overlay
{
    private static readonly ProtoId<ContentTileDefinition> WaterTile = "FloorRogueTownWater";
    private static readonly ProtoId<ContentTileDefinition> YuckwaterTile = "FloorRogueTownYuckwater";

    private const string StateName = "together";
    private const string WaterRsi = "/Textures/_Baseline/Tiles/RogueTown/water_animated.rsi";
    private const string YuckwaterRsi = "/Textures/_Baseline/Tiles/RogueTown/yuckwater_animated.rsi";

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedTransformSystem _transformSystem;
    private List<Entity<MapGridComponent>> _grids = new();

    private readonly Texture[] _waterFrames;
    private readonly Texture[] _yuckwaterFrames;
    private readonly float[] _frameDelays;
    private readonly ushort _waterTileId;
    private readonly ushort _yuckwaterTileId;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public RogueWaterOverlay(IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);

        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.FloorTiles;

        _mapSystem = entityManager.System<SharedMapSystem>();
        _transformSystem = entityManager.System<SharedTransformSystem>();

        _waterTileId = _prototypeManager.Index(WaterTile).TileId;
        _yuckwaterTileId = _prototypeManager.Index(YuckwaterTile).TileId;

        var waterState = GetState(WaterRsi);
        var yuckwaterState = GetState(YuckwaterRsi);
        _waterFrames = waterState.GetFrames(RsiDirection.South);
        _yuckwaterFrames = yuckwaterState.GetFrames(RsiDirection.South);
        _frameDelays = waterState.GetDelays();
    }

    private RSI.State GetState(string path)
    {
        var rsi = _resourceCache.GetResource<RSIResource>(new ResPath(path)).RSI;
        if (!rsi.TryGetState(StateName, out var state))
            throw new InvalidOperationException($"RSI {path} is missing state '{StateName}'.");

        return state;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        var cycleDuration = _frameDelays.Sum();
        var cycleTime = (float) (_timing.RealTime.TotalSeconds % cycleDuration);
        var frame = 0;
        while (cycleTime >= _frameDelays[frame])
        {
            cycleTime -= _frameDelays[frame];
            frame = (frame + 1) % _waterFrames.Length;
        }

        _grids.Clear();
        _mapManager.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids, approx: true);

        foreach (var grid in _grids)
        {
            args.WorldHandle.SetTransform(_transformSystem.GetWorldMatrix(grid.Owner));

            foreach (var tile in _mapSystem.GetTilesIntersecting(grid.Owner, grid, args.WorldAABB))
            {
                Texture? texture = tile.Tile.TypeId switch
                {
                    var id when id == _waterTileId => _waterFrames[frame],
                    var id when id == _yuckwaterTileId => _yuckwaterFrames[frame],
                    _ => null,
                };

                if (texture == null)
                    continue;

                var bottomLeft = (Vector2) tile.GridIndices * grid.Comp.TileSize;
                var bounds = Box2.FromDimensions(bottomLeft, Vector2.One * grid.Comp.TileSize);
                args.WorldHandle.DrawTextureRect(texture, bounds);
            }
        }

        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }
}
