using Content.Shared._Baseline.RogueTown;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Tag;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownMiningWallSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownMiningWallComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
    }

    private void OnBeforeDamageChanged(EntityUid uid, RogueTownMiningWallComponent component, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin is not { } origin)
        {
            args.Cancelled = true;
            return;
        }

        if (_tag.HasTag(origin, component.RequiredToolTag))
            return;

        if (_hands.TryGetActiveItem((origin, CompOrNull<HandsComponent>(origin)), out var held) &&
            _tag.HasTag(held.Value, component.RequiredToolTag))
            return;

        args.Cancelled = true;
    }
}
