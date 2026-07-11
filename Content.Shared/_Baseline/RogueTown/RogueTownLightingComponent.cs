using Robust.Shared.Serialization;

namespace Content.Shared._Baseline.RogueTown;

[RegisterComponent]
public sealed partial class RogueTownTorchComponent : Component
{
}

[RegisterComponent]
public sealed partial class RogueTownTorchHolderComponent : Component
{
    [DataField]
    public string Slot = "torch_slot";

    [DataField]
    public bool SouthFacing;
}

[RegisterComponent]
public sealed partial class RogueTownStoneFireComponent : Component
{
}

[Serializable, NetSerializable]
public enum RogueTownTorchHolderVisuals : byte
{
    State
}

[Serializable, NetSerializable]
public enum RogueTownTorchHolderState : byte
{
    Empty,
    Unlit,
    Lit
}
