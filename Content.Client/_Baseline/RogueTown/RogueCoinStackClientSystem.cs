using Content.Shared._Baseline.RogueTown;
using Content.Shared.Hands;
using Content.Shared.Stacks;

namespace Content.Client._Baseline.RogueTown;

public sealed class RogueCoinStackClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueCoinStackComponent, MoveHeldEntityToActiveHandAttemptEvent>(OnMoveToActiveHandAttempt);
    }

    private void OnMoveToActiveHandAttempt(Entity<RogueCoinStackComponent> ent, ref MoveHeldEntityToActiveHandAttemptEvent args)
    {
        if (TryComp<StackComponent>(ent, out var stack) && stack.Count > 1)
            args.Cancelled = true;
    }
}
