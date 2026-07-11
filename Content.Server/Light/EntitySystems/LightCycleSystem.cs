using Content.Shared;
using Content.Server.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Light.EntitySystems;

/// <inheritdoc/>
public sealed class LightCycleSystem : SharedLightCycleSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    protected override void OnCycleMapInit(Entity<LightCycleComponent> ent, ref MapInitEvent args)
    {
        base.OnCycleMapInit(ent, ref args);

        if (ent.Comp.InitialOffset)
        {
            SetOffset(ent, _random.Next(ent.Comp.Duration));
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            return;

        var query = AllEntityQuery<LightCycleComponent>();
        while (query.MoveNext(out var uid, out var cycle))
        {
            cycle.TimeScale = 1;
            SetOffset((uid, cycle), TimeSpan.Zero);
            Dirty(uid, cycle);
        }
    }

    public int SetPhase(bool day)
    {
        var changed = 0;
        var query = AllEntityQuery<LightCycleComponent>();
        while (query.MoveNext(out var uid, out var cycle))
        {
            var elapsed = GetElapsed(cycle, uid);
            var phase = day
                ? 0d
                : cycle.DayDuration.TotalSeconds + cycle.TransitionDuration.TotalSeconds;
            var calendar = GetCalendarTime(cycle, elapsed);
            var targetTime = day
                ? CalendarStartTime
                : GetCalendarTime(cycle, phase).TimeOfDay;
            var desiredElapsed = GetElapsedSecondsForCalendarTime(cycle, calendar.Day, targetTime);
            var unscaledRoundTime = GetUnscaledRoundTime(uid).TotalSeconds;

            SetOffset((uid, cycle), TimeSpan.FromSeconds(desiredElapsed - unscaledRoundTime * cycle.TimeScale));
            changed++;
        }

        return changed;
    }

    public int SetTimeScale(int timeScale)
    {
        var changed = 0;
        var query = AllEntityQuery<LightCycleComponent>();
        while (query.MoveNext(out var uid, out var cycle))
        {
            var elapsed = GetElapsed(cycle, uid);
            var unscaledRoundTime = GetUnscaledRoundTime(uid).TotalSeconds;

            cycle.TimeScale = timeScale;
            SetOffset((uid, cycle), TimeSpan.FromSeconds(elapsed - unscaledRoundTime * timeScale));
            Dirty(uid, cycle);
            changed++;
        }

        return changed;
    }

    private double GetElapsed(LightCycleComponent cycle, EntityUid uid)
    {
        return GetElapsedSeconds(cycle, _timing.CurTime - _ticker.RoundStartTimeSpan, _metadata.GetPauseTime(uid));
    }

    private TimeSpan GetUnscaledRoundTime(EntityUid uid)
    {
        return _timing.CurTime - _ticker.RoundStartTimeSpan - _metadata.GetPauseTime(uid);
    }
}
