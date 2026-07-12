using Content.Shared._Baseline.RogueTown;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownBleedOnHitSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RogueTownBleedOnHitComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<RogueTownBleedOnHitComponent> weapon, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0 || args.BaseDamage.GetTotal() <= 0)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!_random.Prob(weapon.Comp.BleedChance) || !TryComp<BloodstreamComponent>(target, out var bloodstream))
                continue;

            _bloodstream.TryModifyBleedAmount((target, bloodstream), weapon.Comp.BleedAmount);
        }
    }
}
