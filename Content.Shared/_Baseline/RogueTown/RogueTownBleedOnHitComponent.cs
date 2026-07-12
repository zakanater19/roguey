using Robust.Shared.GameStates;

namespace Content.Shared._Baseline.RogueTown;

/// <summary>
/// Adds a persistent bleeding wound when this melee weapon hits a target with a bloodstream.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RogueTownBleedOnHitComponent : Component
{
    [DataField, AutoNetworkedField]
    public float BleedAmount = 1.25f;

    [DataField, AutoNetworkedField]
    public float BleedChance = 1f;
}
