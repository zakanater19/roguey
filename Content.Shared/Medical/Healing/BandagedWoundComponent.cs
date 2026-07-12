using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Healing;

/// <summary>
/// Marks an already-bandaged wound. Stitching a later bleeding wound clears this marker.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BandagedWoundComponent : Component;
