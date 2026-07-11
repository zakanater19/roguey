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
    public ProtoId<TagPrototype>? AlternativeToolTag;

    /// <summary>
    /// Optional damage applied by the required tool before structural resistances.
    /// </summary>
    [DataField]
    public float? ToolDamage;

    /// <summary>
    /// Optional damage applied by the alternative tool before structural resistances.
    /// </summary>
    [DataField]
    public float? AlternativeToolDamage;

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
