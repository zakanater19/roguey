using Content.Shared._Baseline.RogueTown;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownBushSystem : EntitySystem
{
    private static readonly EntProtoId[] HarvestPrototypes =
    {
        "RogueTownBerries",
        "RogueTownFibers",
        "RogueTownThorn",
    };

    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownBushComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnInteractHand(Entity<RogueTownBushComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.HarvestsRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("roguetown-bush-picked-clean"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.HarvestsRemaining--;
        var harvest = Spawn(_random.Pick(HarvestPrototypes), Transform(args.User).Coordinates);
        _hands.TryPickupAnyHand(args.User, harvest);
        _popup.PopupEntity(Loc.GetString("roguetown-bush-harvest"), ent, args.User);
    }
}
