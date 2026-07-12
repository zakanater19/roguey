using Content.Shared.Alert;
using Robust.Shared.GameStates;

namespace Content.Shared.Bed.Sleep;

/// <summary>
/// Tracks the deliberate ten-second transition into or out of sleep.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class SleepTransitionComponent : Component
{
    [DataField, AutoNetworkedField]
    public SleepTransitionPhase Phase;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan EndsAt;
}

public enum SleepTransitionPhase : byte
{
    FallingAsleep,
    WakingUp,
}

/// <summary>
/// Raised when a player begins a sleep transition so the server can put feedback in their chat box.
/// </summary>
[ByRefEvent]
public record struct SleepTransitionStartedEvent(SleepTransitionPhase Phase);

public sealed partial class ToggleSleepAlertEvent : BaseAlertEvent;
