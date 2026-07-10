using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Client.Items.Systems;

public sealed class ItemSystem : SharedItemSystem
{
    private const float FallbackInhandScale = 0.75f;

    private static readonly Vector2 FallbackLeftInhandOffset = new(0.09375f, -0.125f);
    private static readonly Vector2 FallbackRightInhandOffset = new(-0.09375f, -0.125f);
    private static readonly Vector2 FallbackMiddleInhandOffset = new(0f, -0.125f);

    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, GetInhandVisualsEvent>(OnGetVisuals);

        // TODO is this still needed? Shouldn't containers occlude them?
        SubscribeLocalEvent<SpriteComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<SpriteComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnUnequipped(EntityUid uid, SpriteComponent component, GotUnequippedEvent args)
    {
        _sprite.SetVisible((uid, component), true);
    }

    private void OnEquipped(EntityUid uid, SpriteComponent component, GotEquippedEvent args)
    {
        _sprite.SetVisible((uid, component), false);
    }

    #region InhandVisuals

    /// <summary>
    ///     When an items visual state changes, notify and entities that are holding this item that their sprite may need updating.
    /// </summary>
    public override void VisualsChanged(EntityUid uid)
    {
        // if the item is in a container, it might be equipped to hands or inventory slots --> update visuals.
        if (Container.TryGetContainingContainer((uid, null, null), out var container))
            RaiseLocalEvent(container.Owner, new VisualsChangedEvent(GetNetEntity(uid), container.ID));
    }

    /// <summary>
    ///     An entity holding this item is requesting visual information for in-hand sprites.
    /// </summary>
    private void OnGetVisuals(EntityUid uid, ItemComponent item, GetInhandVisualsEvent args)
    {
        var defaultKey = $"inhand-{args.Location.ToString().ToLowerInvariant()}";

        // try get explicit visuals
        if (!item.InhandVisuals.TryGetValue(args.Location, out var layers))
        {
            // get defaults
            if (!TryGetDefaultVisuals(uid, item, defaultKey, args.Location, out layers))
                return;
        }

        var i = 0;
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = i == 0 ? defaultKey : $"{defaultKey}-{i}";
                i++;
            }

            args.Layers.Add((key, layer));
        }
    }

    /// <summary>
    ///     If no explicit in-hand visuals were specified, this attempts to populate with default values.
    /// </summary>
    /// <remarks>
    ///     Useful for lazily adding in-hand sprites without modifying yaml, and for falling back to
    ///     half-sized world sprites when no custom in-hand sprite exists.
    /// </remarks>
    private bool TryGetDefaultVisuals(EntityUid uid, ItemComponent item, string defaultKey, HandLocation location, [NotNullWhen(true)] out List<PrototypeLayerData>? result)
    {
        result = null;

        RSI? rsi = null;

        if (item.RsiPath != null)
            rsi = _resCache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / item.RsiPath).RSI;
        else if (TryComp(uid, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return TryGetFallbackVisuals(uid, null, defaultKey, location, out result);

        var state = (item.HeldPrefix == null)
            ? defaultKey
            : $"{item.HeldPrefix}-{defaultKey}";

        if (!rsi.TryGetState(state, out _))
            return TryGetFallbackVisuals(uid, rsi, defaultKey, location, out result);

        var layer = new PrototypeLayerData();
        layer.RsiPath = rsi.Path.ToString();
        layer.State = state;
        layer.MapKeys = new() { state };

        result = new() { layer };
        return true;
    }

    private bool TryGetFallbackVisuals(EntityUid uid, RSI? rsi, string defaultKey, HandLocation location, [NotNullWhen(true)] out List<PrototypeLayerData>? result)
    {
        result = null;

        if (TryComp(uid, out SpriteComponent? sprite))
        {
            var layers = new List<PrototypeLayerData>();
            var fallbackOffset = GetFallbackInhandOffset(location);

            foreach (var spriteLayer in sprite.AllLayers)
            {
                if (!spriteLayer.Visible || !spriteLayer.RsiState.IsValid)
                    continue;

                var key = layers.Count == 0 ? defaultKey : $"{defaultKey}-fallback-{layers.Count}";
                var layer = new PrototypeLayerData
                {
                    RsiPath = spriteLayer.Rsi?.Path.CanonPath,
                    State = spriteLayer.RsiState.Name,
                    Color = spriteLayer.Color,
                    Rotation = spriteLayer.Rotation,
                    Scale = spriteLayer.Scale * sprite.Scale * FallbackInhandScale,
                    Visible = spriteLayer.Visible,
                    Offset = fallbackOffset,
                    MapKeys = new() { key },
                };

                if (spriteLayer is SpriteComponent.Layer concreteLayer)
                {
                    layer.Shader = concreteLayer.ShaderPrototype;
                    layer.RenderingStrategy = concreteLayer.RenderingStrategy;
                    layer.Offset += concreteLayer.Offset + sprite.Offset;
                }

                layers.Add(layer);
            }

            if (layers.Count > 0)
            {
                result = layers;
                return true;
            }
        }

        if (rsi == null || !rsi.TryGetState("icon", out _))
            return false;

        result = new()
        {
            new PrototypeLayerData
            {
                RsiPath = rsi.Path.ToString(),
                State = "icon",
                Scale = new Vector2(FallbackInhandScale, FallbackInhandScale),
                Offset = GetFallbackInhandOffset(location),
                MapKeys = new() { defaultKey },
            }
        };

        return true;
    }

    private static Vector2 GetFallbackInhandOffset(HandLocation location)
    {
        return location switch
        {
            HandLocation.Left => FallbackLeftInhandOffset,
            HandLocation.Right => FallbackRightInhandOffset,
            _ => FallbackMiddleInhandOffset,
        };
    }
    #endregion
}
