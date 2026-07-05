using Content.Server.Administration;
using Content.Server.Stack;
using Content.Shared._Baseline.RogueTown;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Player;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueCoinStackSplitSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueCoinStackComponent, MoveHeldEntityToActiveHandAttemptEvent>(OnMoveToActiveHandAttempt);
    }

    private void OnMoveToActiveHandAttempt(Entity<RogueCoinStackComponent> ent, ref MoveHeldEntityToActiveHandAttemptEvent args)
    {
        if (!TryComp<StackComponent>(ent, out var stack) || stack.Count <= 1)
            return;

        args.Cancelled = true;

        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !TryComp<HandsComponent>(args.User, out var hands) ||
            hands.ActiveHandId != args.TargetHand ||
            !_hands.HandIsEmpty((args.User, hands), args.TargetHand) ||
            !_hands.CanDropHeld(args.User, args.SourceHand))
        {
            return;
        }

        var max = stack.Count - 1;
        var stackUid = ent.Owner;
        var user = args.User;
        var sourceHand = args.SourceHand;
        var targetHand = args.TargetHand;
        _quickDialog.OpenDialog(
            actor.PlayerSession,
            Loc.GetString("rogue-coin-split-title"),
            Loc.GetString("rogue-coin-split-prompt", ("max", max)),
            (int amount) => SplitToActiveHand(stackUid, user, sourceHand, targetHand, amount));
    }

    private void SplitToActiveHand(EntityUid stackUid, EntityUid user, string sourceHand, string targetHand, int amount)
    {
        if (!TryComp<StackComponent>(stackUid, out var stack) ||
            !TryComp<HandsComponent>(user, out var hands) ||
            !_hands.TryGetHeldItem((user, hands), sourceHand, out var held) ||
            held.Value != stackUid ||
            hands.ActiveHandId != targetHand ||
            !_hands.HandIsEmpty((user, hands), targetHand))
        {
            return;
        }

        if (amount <= 0)
        {
            _popup.PopupCursor(Loc.GetString("comp-stack-split-too-small"), user, PopupType.Medium);
            return;
        }

        amount = Math.Min(amount, stack.Count - 1);
        if (amount <= 0)
            return;

        if (_stack.Split((stackUid, stack), amount, Transform(user).Coordinates) is not { } split)
            return;

        _hands.PickupOrDrop(user, split, handsComp: hands);
        _popup.PopupCursor(Loc.GetString("rogue-coin-split-done", ("amount", amount)), user);
    }
}
