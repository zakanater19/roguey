namespace Content.Shared._Baseline.RogueTown;

/// <summary>
/// Tracks the finite number of items that can be picked from a RogueTown bush.
/// </summary>
[RegisterComponent]
public sealed partial class RogueTownBushComponent : Component
{
    [DataField]
    public int HarvestsRemaining = 3;
}
