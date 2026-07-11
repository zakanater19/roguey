using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Baseline.RogueTown;

[UsedImplicitly]
public sealed class RogueWaterOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlayManager.AddOverlay(new RogueWaterOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<RogueWaterOverlay>();
    }
}
