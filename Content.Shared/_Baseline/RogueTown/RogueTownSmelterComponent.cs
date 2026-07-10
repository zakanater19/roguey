using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Baseline.RogueTown;

[RegisterComponent]
public sealed partial class RogueTownSmelterComponent : Component
{
    [DataField]
    public string CoalSlotId = "coal_slot";

    [DataField]
    public string OreSlotId = "ore_slot";

    [DataField]
    public EntProtoId Product = "RogueTownIronIngot";

    [DataField]
    public TimeSpan SmeltTime = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public bool Burning;

    [ViewVariables]
    public TimeSpan FinishTime;
}

[Serializable, NetSerializable]
public enum RogueTownSmelterVisuals : byte
{
    Burning
}

[Serializable, NetSerializable]
public enum RogueTownSmelterVisualLayers : byte
{
    Base
}
