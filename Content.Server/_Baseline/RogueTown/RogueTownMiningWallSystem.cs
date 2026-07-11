using Content.Shared._Baseline.RogueTown;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Destructible;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Random;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownMiningWallSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownMiningWallComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<RogueTownMiningWallComponent, DestructionEventArgs>(OnDestruction);
    }

    private void OnBeforeDamageChanged(EntityUid uid, RogueTownMiningWallComponent component, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is not { } origin)
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<StaminaComponent>(origin, out var stamina) && stamina.Exhausted)
        {
            args.Cancelled = true;
            return;
        }

        var validTool = _tag.HasTag(origin, component.RequiredToolTag);
        EntityUid? heldTool = null;

        if (_hands.TryGetActiveItem((origin, CompOrNull<HandsComponent>(origin)), out var held))
        {
            heldTool = held.Value;
            validTool |= _tag.HasTag(held.Value, component.RequiredToolTag);
        }

        if (!validTool)
        {
            args.Cancelled = true;
            return;
        }

        if (args.Damage.GetTotal() > 0)
            _stamina.TakeStaminaDamage(origin, component.StaminaCost, with: heldTool, visual: false);
    }

    private void OnDestruction(EntityUid uid, RogueTownMiningWallComponent component, DestructionEventArgs args)
    {
        if (component.ExtraDrops.Count == 0)
            return;

        var coords = Transform(uid).Coordinates;

        foreach (var drop in component.ExtraDrops)
        {
            if (!_random.Prob(drop.Probability))
                continue;

            Spawn(drop.Entity, coords.Offset(_random.NextVector2(0.2f)));
        }
    }
}
