using Content.Shared.Maps;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Baseline.RogueTown;

[RegisterComponent, NetworkedComponent]
public sealed partial class RogueTownReplaceTileOnDestroyedComponent : Component
{
    [DataField("tile", required: true)]
    public ProtoId<ContentTileDefinition> Tile { get; set; }
}
