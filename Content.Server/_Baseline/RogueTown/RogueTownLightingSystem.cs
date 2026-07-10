using Content.Shared._Baseline.RogueTown;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Server._Baseline.RogueTown;

public sealed class RogueTownLightingSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLights = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RogueTownTorchHolderComponent, ComponentStartup>(OnHolderStartup);
        SubscribeLocalEvent<RogueTownTorchHolderComponent, MapInitEvent>(OnHolderMapInit);
        SubscribeLocalEvent<RogueTownTorchHolderComponent, EntInsertedIntoContainerMessage>(OnHolderInserted);
        SubscribeLocalEvent<RogueTownTorchHolderComponent, EntRemovedFromContainerMessage>(OnHolderRemoved);

        SubscribeLocalEvent<RogueTownStoneFireComponent, InteractHandEvent>(OnStoneFireHandInteract);
        SubscribeLocalEvent<RogueTownStoneFireComponent, InteractUsingEvent>(OnStoneFireUsingInteract);
    }

    private void OnHolderStartup(Entity<RogueTownTorchHolderComponent> ent, ref ComponentStartup args)
    {
        SetHolderState(ent, null);
    }

    private void OnHolderMapInit(Entity<RogueTownTorchHolderComponent> ent, ref MapInitEvent args)
    {
        SetHolderState(ent, _itemSlots.GetItemOrNull(ent, ent.Comp.Slot));
    }

    private void OnHolderInserted(Entity<RogueTownTorchHolderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot)
            return;

        SetHolderState(ent, args.Entity);
    }

    private void OnHolderRemoved(Entity<RogueTownTorchHolderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot)
            return;

        SetHolderState(ent, null);
    }

    private void SetHolderState(Entity<RogueTownTorchHolderComponent> ent, EntityUid? torch)
    {
        var state = RogueTownTorchHolderState.Empty;
        if (torch != null)
        {
            state = TryComp<ItemToggleComponent>(torch, out var toggle) && toggle.Activated
                ? RogueTownTorchHolderState.Lit
                : RogueTownTorchHolderState.Unlit;
        }

        _appearance.SetData(ent, RogueTownTorchHolderVisuals.State, state);
        _pointLights.SetEnabled(ent, state == RogueTownTorchHolderState.Lit);
    }

    private void OnStoneFireHandInteract(Entity<RogueTownStoneFireComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !_itemToggle.IsActivated(ent.Owner))
            return;

        _itemToggle.TryDeactivate(ent.Owner, args.User, predicted: false, showPopup: false);
        _popup.PopupEntity("You snuff out the flame.", ent, args.User);
        args.Handled = true;
    }

    private void OnStoneFireUsingInteract(Entity<RogueTownStoneFireComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<RogueTownTorchComponent>(args.Used))
            return;

        if (!_itemToggle.IsActivated(args.Used))
        {
            _popup.PopupEntity("The torch is not lit.", ent, args.User);
            args.Handled = true;
            return;
        }

        if (_itemToggle.IsActivated(ent.Owner))
        {
            _popup.PopupEntity("The brazier is already lit.", ent, args.User);
            args.Handled = true;
            return;
        }

        _itemToggle.TryActivate(ent.Owner, args.User, predicted: false, showPopup: false);
        _popup.PopupEntity("You relight the brazier.", ent, args.User);
        args.Handled = true;
    }
}
