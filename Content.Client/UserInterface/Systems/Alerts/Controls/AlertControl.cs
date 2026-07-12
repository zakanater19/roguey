using System.Numerics;
using Content.Client.Actions.UI;
using Content.Client.Cooldown;
using Content.Shared.Alert;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Alerts.Controls
{
    public sealed class AlertControl : BaseButton
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IPlayerManager _player = default!;

        private readonly SpriteSystem _sprite;

        public AlertPrototype Alert { get; }

        /// <summary>
        /// Current cooldown displayed in this slot. Set to null to show no cooldown.
        /// </summary>
        public (TimeSpan Start, TimeSpan End)? Cooldown
        {
            get => _cooldown;
            set
            {
                _cooldown = value;
                if (SuppliedTooltip is ActionAlertTooltip actionAlertTooltip)
                {
                    actionAlertTooltip.Cooldown = value;
                }
            }
        }

        private (TimeSpan Start, TimeSpan End)? _cooldown;

        private short? _severity;

        private readonly SpriteView _icon;
        private readonly CooldownGraphic _cooldownGraphic;
        private readonly Label? _healthLabel;

        private EntityUid _spriteViewEntity;

        /// <summary>
        /// Creates an alert control reflecting the indicated alert + state
        /// </summary>
        /// <param name="alert">alert to display</param>
        /// <param name="severity">severity of alert, null if alert doesn't have severity levels</param>
        public AlertControl(AlertPrototype alert, short? severity)
        {
            // Alerts will handle this.
            MuteSounds = true;

            IoCManager.InjectDependencies(this);
            _sprite = _entityManager.System<SpriteSystem>();
            TooltipSupplier = SupplyTooltip;
            Alert = alert;

            HorizontalAlignment = HAlignment.Left;
            _severity = severity;
            _icon = new SpriteView
            {
                Scale = new Vector2(2, 2),
                MaxSize = new Vector2(64, 64),
                Stretch = SpriteView.StretchMode.None,
                HorizontalAlignment = HAlignment.Left
            };

            SetupIcon();

            Children.Add(_icon);
            if (Alert.ID is "HumanHealth" or "HumanCrit")
            {
                _icon.Visible = false;
                _healthLabel = new Label
                {
                    MinSize = new Vector2(64, 64),
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    Align = Label.AlignMode.Center,
                    FontColorOverride = Color.White,
                };
                Children.Add(_healthLabel);
            }

            _cooldownGraphic = new CooldownGraphic
            {
                MaxSize = new Vector2(64, 64)
            };
            Children.Add(_cooldownGraphic);
        }

        private Control SupplyTooltip(Control? sender)
        {
            var msg = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Name));
            var desc = FormattedMessage.FromMarkupOrThrow(Loc.GetString(Alert.Description));
            return new ActionAlertTooltip(msg, desc) { Cooldown = Cooldown };
        }

        /// <summary>
        /// Change the alert severity, changing the displayed icon
        /// </summary>
        public void SetSeverity(short? severity)
        {
            if (_severity == severity)
                return;
            _severity = severity;

            if (!_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
                return;
            var icon = Alert.GetIcon(_severity);
            if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            UserInterfaceManager.GetUIController<AlertsUIController>().UpdateAlertSpriteEntity(_spriteViewEntity, Alert);
            UpdateHealthText();

            if (!Cooldown.HasValue)
            {
                _cooldownGraphic.Visible = false;
                _cooldownGraphic.Progress = 0;
                return;
            }

            _cooldownGraphic.FromTime(Cooldown.Value.Start, Cooldown.Value.End);
        }

        private void UpdateHealthText()
        {
            if (_healthLabel == null || _player.LocalEntity is not { } player)
                return;

            if (Alert.ID == "HumanCrit")
            {
                _healthLabel.Text = "Dying";
                _healthLabel.FontColorOverride = Color.Red;
                return;
            }

            if (!_entityManager.TryGetComponent<DamageableComponent>(player, out var damage) ||
                !_entityManager.System<MobThresholdSystem>().TryGetThresholdForState(player, MobState.Critical, out var threshold))
                return;

            var percent = 100 - (int) MathF.Round(damage.TotalDamage.Float() / threshold.Value.Float() * 100f);
            percent = Math.Clamp(percent, 0, 100);
            _healthLabel.Text = $"{percent}%";
            _healthLabel.FontColorOverride = percent > 60 ? Color.LimeGreen : percent > 30 ? Color.Yellow : Color.Red;
        }

        private void SetupIcon()
        {
            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);

            _spriteViewEntity = _entityManager.Spawn(Alert.AlertViewEntity);
            if (_entityManager.TryGetComponent<SpriteComponent>(_spriteViewEntity, out var sprite))
            {
                var icon = Alert.GetIcon(_severity);
                if (_sprite.LayerMapTryGet((_spriteViewEntity, sprite), AlertVisualLayers.Base, out var layer, false))
                    _sprite.LayerSetSprite((_spriteViewEntity, sprite), layer, icon);
            }

            _icon.SetEntity(_spriteViewEntity);
        }

        protected override void EnteredTree()
        {
            base.EnteredTree();
            SetupIcon();
        }

        protected override void ExitedTree()
        {
            base.ExitedTree();

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!_entityManager.Deleted(_spriteViewEntity))
                _entityManager.QueueDeleteEntity(_spriteViewEntity);
        }
    }

    public enum AlertVisualLayers : byte
    {
        Base
    }
}
