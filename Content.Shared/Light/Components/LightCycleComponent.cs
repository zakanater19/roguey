using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Shared.Light.Components;

/// <summary>
/// Cycles through colors AKA "Day / Night cycle" on <see cref="MapLightComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LightCycleComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color OriginalColor = Color.Transparent;

    /// <summary>
    /// How long an entire cycle lasts
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromMinutes(40);

    /// <summary>
    /// How long daylight lasts before night begins.
    /// </summary>
    [DataField, AutoNetworkedField]
    // The cycle starts at 06:12. 24:40 later is 21:00, when dusk begins.
    public TimeSpan DayDuration = TimeSpan.FromMinutes(24) + TimeSpan.FromSeconds(40);

    /// <summary>
    /// The final part of day/night used to fade into the next phase.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TransitionDuration = TimeSpan.FromMinutes(2);

    [DataField, AutoNetworkedField]
    public TimeSpan Offset;

    /// <summary>
    /// Multiplier applied to the passage of round time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int TimeScale = 1;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Should the offset be randomised upon MapInit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InitialOffset = false;

    /// <summary>
    /// Ambient light at night. The original map light color is used for daytime.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color NightColor = Color.Black;

    /// <summary>
    /// Trench of the oscillation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinLightLevel = 0f;

    /// <summary>
    /// Peak of the oscillation
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxLightLevel = 3f;

    [DataField, AutoNetworkedField]
    public float ClipLight = 1.25f;

    [DataField, AutoNetworkedField]
    public Color ClipLevel = new Color(1f, 1f, 1.25f);

    [DataField, AutoNetworkedField]
    public Color MinLevel = new Color(0.1f, 0.15f, 0.50f);

    [DataField, AutoNetworkedField]
    public Color MaxLevel = new Color(2f, 2f, 5f);
}
