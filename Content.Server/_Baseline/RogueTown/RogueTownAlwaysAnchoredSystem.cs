using Content.Shared._Baseline.RogueTown;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownAlwaysAnchoredSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RogueTownAlwaysAnchoredComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
    }

    private void OnAnchorStateChanged(
        Entity<RogueTownAlwaysAnchoredComponent> ent,
        ref AnchorStateChangedEvent args)
    {
        if (args.Anchored || args.Detaching || TerminatingOrDeleted(ent))
            return;

        _transform.AnchorEntity(ent.Owner, args.Transform);
    }
}
