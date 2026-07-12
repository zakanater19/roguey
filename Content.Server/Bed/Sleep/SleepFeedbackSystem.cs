using Content.Server.Chat.Managers;
using Content.Shared.Bed.Sleep;
using Robust.Shared.Player;

namespace Content.Server.Bed.Sleep;

/// <summary>
/// Sends private sleep-transition feedback to the player's chat box.
/// </summary>
public sealed class SleepFeedbackSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, SleepTransitionStartedEvent>(OnTransitionStarted);
    }

    private void OnTransitionStarted(Entity<ActorComponent> ent, ref SleepTransitionStartedEvent args)
    {
        var message = Loc.GetString(args.Phase == SleepTransitionPhase.FallingAsleep
            ? "sleep-transition-falling"
            : "sleep-transition-waking");
        _chat.DispatchServerMessage(ent.Comp.PlayerSession, message);
    }
}
