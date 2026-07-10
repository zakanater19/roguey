using Content.Shared._Baseline.RogueTown;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownSmelterSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownSmelterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RogueTownSmelterComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnStartup(EntityUid uid, RogueTownSmelterComponent component, ComponentStartup args)
    {
        SetBurning(uid, component, false);
    }

    private void OnInteractHand(EntityUid uid, RogueTownSmelterComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (component.Burning)
        {
            _popup.PopupClient("The smelter is already burning.", uid, args.User);
            args.Handled = true;
            return;
        }

        if (!HasIngredients(uid, component))
        {
            _popup.PopupClient("The smelter needs coal and iron ore.", uid, args.User);
            args.Handled = true;
            return;
        }

        StartSmelting(uid, component);
        args.Handled = true;
    }

    private bool HasIngredients(EntityUid uid, RogueTownSmelterComponent component)
    {
        return _itemSlots.GetItemOrNull(uid, component.CoalSlotId) != null &&
               _itemSlots.GetItemOrNull(uid, component.OreSlotId) != null;
    }

    private void StartSmelting(EntityUid uid, RogueTownSmelterComponent component)
    {
        component.FinishTime = _timing.CurTime + component.SmeltTime;
        SetBurning(uid, component, true);
    }

    private void FinishSmelting(EntityUid uid, RogueTownSmelterComponent component)
    {
        var coal = _itemSlots.GetItemOrNull(uid, component.CoalSlotId);
        var ore = _itemSlots.GetItemOrNull(uid, component.OreSlotId);

        if (coal != null)
            QueueDel(coal.Value);

        if (ore != null)
            QueueDel(ore.Value);

        Spawn(component.Product, Transform(uid).Coordinates.Offset(_random.NextVector2(0.2f)));
        SetBurning(uid, component, false);
    }

    private void SetBurning(EntityUid uid, RogueTownSmelterComponent component, bool burning)
    {
        component.Burning = burning;
        _itemSlots.SetLock(uid, component.CoalSlotId, burning);
        _itemSlots.SetLock(uid, component.OreSlotId, burning);
        _appearance.SetData(uid, RogueTownSmelterVisuals.Burning, burning);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RogueTownSmelterComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Burning || component.FinishTime > _timing.CurTime)
                continue;

            FinishSmelting(uid, component);
        }
    }
}
