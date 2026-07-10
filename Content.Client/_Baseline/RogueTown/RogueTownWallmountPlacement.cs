using System.Numerics;
using Robust.Client.Placement;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._Baseline.RogueTown;

public abstract class RogueTownWallmountPlacement : PlacementMode
{
    private readonly Direction _direction;

    protected RogueTownWallmountPlacement(PlacementManager placementManager, Direction direction)
        : base(placementManager)
    {
        _direction = direction;
    }

    public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
    {
        pManager.Direction = _direction;
        MouseCoords = ScreenToCursorGrid(mouseScreen);
        CurrentTile = GetTileRef(MouseCoords);

        if (pManager.CurrentPermission!.IsTile)
            return;

        var coordinates = new EntityCoordinates(MouseCoords.EntityId, CurrentTile.GridIndices);
        var offset = _direction switch
        {
            // North/south states are centered horizontally and belong on the wall edge.
            // East/west states contain their own horizontal edge offset and stay in the adjacent tile.
            Direction.North => new Vector2(0.5f, 1f),
            // The south-facing source state reaches toward the tile's upper edge.
            Direction.South => new Vector2(0.5f, 0.49f),
            Direction.East => new Vector2(1.5f, 0.5f),
            Direction.West => new Vector2(-0.5f, 0.5f),
            _ => Vector2.Zero
        };

        MouseCoords = coordinates.Offset(offset);
    }

    public override bool IsValidPosition(EntityCoordinates position)
    {
        return !pManager.CurrentPermission!.IsTile && RangeCheck(position);
    }
}

public sealed class RogueTownWallmountNorth(PlacementManager placementManager)
    : RogueTownWallmountPlacement(placementManager, Direction.North)
{
}

public sealed class RogueTownWallmountSouth(PlacementManager placementManager)
    : RogueTownWallmountPlacement(placementManager, Direction.South)
{
}

public sealed class RogueTownWallmountEast(PlacementManager placementManager)
    : RogueTownWallmountPlacement(placementManager, Direction.East)
{
}

public sealed class RogueTownWallmountWest(PlacementManager placementManager)
    : RogueTownWallmountPlacement(placementManager, Direction.West)
{
}
