using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Baseline.RogueTown;

[RegisterComponent, NetworkedComponent]
public sealed partial class RogueTownMiningWallComponent : Component
{
    [DataField]
    public ProtoId<TagPrototype> RequiredToolTag = "Pickaxe";

    [DataField]
    public float StaminaCost = 5f;

    [DataField]
    public List<RogueTownMiningDropEntry> ExtraDrops = new();
}

[DataDefinition]
public sealed partial class RogueTownMiningDropEntry
{
    [DataField(required: true)]
    public EntProtoId Entity;

    [DataField]
    public float Probability = 1f;
}
