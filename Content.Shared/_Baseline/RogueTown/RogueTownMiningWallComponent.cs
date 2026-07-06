using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Baseline.RogueTown;

[RegisterComponent, NetworkedComponent]
public sealed partial class RogueTownMiningWallComponent : Component
{
    [DataField]
    public ProtoId<TagPrototype> RequiredToolTag = "Pickaxe";
}
