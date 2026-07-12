using Content.Client.Movement.Systems;
using Content.Shared.Bed.Sleep;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Eye.Blinding;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;

namespace Content.Client.Eye.Blinding
{
    public sealed class BlindOverlay : Overlay
    {
        private static readonly ProtoId<ShaderPrototype> GreyscaleShader = "GreyscaleFullscreen";
        private static readonly ProtoId<ShaderPrototype> CircleShader = "CircleMask";

        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly ILightManager _lightManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        public override bool RequestScreenTexture => true;
        public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;
        private readonly ShaderInstance _greyscaleShader;
        private readonly ShaderInstance _circleMaskShader;

        private BlindableComponent _blindableComponent = default!;
        private float _sleepOpacity;

        public BlindOverlay()
        {
            IoCManager.InjectDependencies(this);
            _greyscaleShader = _prototypeManager.Index(GreyscaleShader).InstanceUnique();
            _circleMaskShader = _prototypeManager.Index(CircleShader).InstanceUnique();
        }
        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            if (!_entityManager.TryGetComponent(_playerManager.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
                return false;

            if (args.Viewport.Eye != eyeComp.Eye)
                return false;

            var playerEntity = _playerManager.LocalSession?.AttachedEntity;

            if (playerEntity == null)
                return false;

            _sleepOpacity = 0f;
            if (_entityManager.HasComponent<SleepingComponent>(playerEntity.Value))
            {
                _sleepOpacity = 1f;
            }
            else if (_entityManager.TryGetComponent<SleepTransitionComponent>(playerEntity.Value, out var transition) &&
                     transition.Phase == SleepTransitionPhase.FallingAsleep)
            {
                var duration = (transition.EndsAt - transition.StartedAt).TotalSeconds;
                if (duration > 0)
                    _sleepOpacity = Math.Clamp((float) ((_timing.CurTime - transition.StartedAt).TotalSeconds / duration), 0f, 1f);
            }

            // The sleep fade is screen-space only. It must never draw into world-space or it can bypass the FOV mask.
            if (_sleepOpacity > 0f)
                return args.Space == OverlaySpace.ScreenSpace;

            if (args.Space != OverlaySpace.WorldSpace)
                return false;

            if (!_entityManager.TryGetComponent<BlindableComponent>(playerEntity, out var blindComp))
                return false;

            _blindableComponent = blindComp;

            var blind = _blindableComponent.IsBlind;

            if (!blind && _blindableComponent.LightSetup) // Turn FOV back on if we can see again
            {
                _lightManager.Enabled = true;
                _blindableComponent.LightSetup = false;
                _blindableComponent.GraceFrame = true;
                return true;
            }

            return blind;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            var playerEntity = _playerManager.LocalSession?.AttachedEntity;

            if (playerEntity == null)
                return;

            if (_sleepOpacity > 0f)
            {
                args.ScreenHandle.DrawRect(args.ViewportBounds, Color.Black.WithAlpha(_sleepOpacity));
                return;
            }

            if (ScreenTexture == null)
                return;

            if (!_blindableComponent.GraceFrame)
            {
                _blindableComponent.LightSetup = true; // Ok we touched the lights
                _lightManager.Enabled = false;
            } else
            {
                _blindableComponent.GraceFrame = false;
            }

            if (_entityManager.TryGetComponent<EyeComponent>(playerEntity, out var content))
            {
                _circleMaskShader?.SetParameter("Zoom", content.Zoom.X);
            }

            _greyscaleShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);

            var worldHandle = args.WorldHandle;
            var viewport = args.WorldBounds;

            worldHandle.UseShader(_greyscaleShader);
            worldHandle.DrawRect(viewport, Color.White);
            worldHandle.UseShader(_circleMaskShader);
            worldHandle.DrawRect(viewport, Color.White);
            worldHandle.UseShader(null);
        }
    }
}
