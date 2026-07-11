using Content.Shared.Light.Components;
using Robust.Shared.Map.Components;

namespace Content.Shared.Light.EntitySystems;

public abstract class SharedLightCycleSystem : EntitySystem
{
    public static readonly TimeSpan CalendarStartTime = new(6, 12, 0);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LightCycleComponent, MapInitEvent>(OnCycleMapInit);
        SubscribeLocalEvent<LightCycleComponent, ComponentShutdown>(OnCycleShutdown);
    }

    protected virtual void OnCycleMapInit(Entity<LightCycleComponent> ent, ref MapInitEvent args)
    {
        if (TryComp(ent.Owner, out MapLightComponent? mapLight))
        {
            ent.Comp.OriginalColor = mapLight.AmbientLightColor;
            Dirty(ent);
        }
    }

    private void OnCycleShutdown(Entity<LightCycleComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp(ent.Owner, out MapLightComponent? mapLight))
        {
            mapLight.AmbientLightColor = ent.Comp.OriginalColor;
            Dirty(ent.Owner, mapLight);
        }
    }

    public void SetOffset(Entity<LightCycleComponent> entity, TimeSpan offset)
    {
        entity.Comp.Offset = offset;
        var ev = new LightCycleOffsetEvent(offset);

        RaiseLocalEvent(entity, ref ev);
        Dirty(entity);
    }

    public static Color GetColor(Entity<LightCycleComponent> cycle, Color color, double time)
    {
        if (!cycle.Comp.Enabled)
            return color;

        var duration = Math.Max(1d, cycle.Comp.Duration.TotalSeconds);
        var dayDuration = Math.Clamp(cycle.Comp.DayDuration.TotalSeconds, 0d, duration);
        var transition = Math.Clamp(cycle.Comp.TransitionDuration.TotalSeconds,
            0d,
            Math.Min(dayDuration, duration - dayDuration));
        var cycleTime = Mod(time, duration);

        if (transition <= 0d)
            return cycleTime < dayDuration ? color : cycle.Comp.NightColor;

        if (cycleTime < dayDuration - transition)
            return color;

        if (cycleTime < dayDuration)
        {
            var progress = (float) ((cycleTime - (dayDuration - transition)) / transition);
            return Color.InterpolateBetween(color, cycle.Comp.NightColor, SmoothStep(progress));
        }

        if (cycleTime < duration - transition)
            return cycle.Comp.NightColor;

        var dawnProgress = (float) ((cycleTime - (duration - transition)) / transition);
        return Color.InterpolateBetween(cycle.Comp.NightColor, color, SmoothStep(dawnProgress));
    }

    public static double GetElapsedSeconds(LightCycleComponent cycle, TimeSpan roundTime, TimeSpan pausedTime)
    {
        return (roundTime - pausedTime).TotalSeconds * cycle.TimeScale + cycle.Offset.TotalSeconds;
    }

    public static DayNightCalendarTime GetCalendarTime(LightCycleComponent cycle, double elapsedSeconds)
    {
        var duration = Math.Max(1d, cycle.Duration.TotalSeconds);
        var cycleTime = Mod(elapsedSeconds, duration);
        var worldDaySeconds = TimeSpan.FromDays(1).TotalSeconds;
        var absoluteWorldSeconds = CalendarStartTime.TotalSeconds + elapsedSeconds / duration * worldDaySeconds;
        var day = Math.Max(1, (int) Math.Floor(absoluteWorldSeconds / worldDaySeconds) + 1);

        // A complete forty-minute cycle represents a full in-world day. Starting at 06:12 means the
        // two-minute dawn fade begins at 05:00 and has just completed when a new day starts.
        var secondsSinceMidnight = Mod(CalendarStartTime.TotalSeconds +
            cycleTime / duration * worldDaySeconds,
            worldDaySeconds);

        return new DayNightCalendarTime(day,
            TimeSpan.FromSeconds(secondsSinceMidnight),
            cycleTime < cycle.DayDuration.TotalSeconds);
    }

    public static double GetElapsedSecondsForCalendarTime(LightCycleComponent cycle, int day, TimeSpan timeOfDay)
    {
        var worldDaySeconds = TimeSpan.FromDays(1).TotalSeconds;
        var absoluteWorldSeconds = (day - 1) * worldDaySeconds + timeOfDay.TotalSeconds;
        return (absoluteWorldSeconds - CalendarStartTime.TotalSeconds) /
            worldDaySeconds * Math.Max(1d, cycle.Duration.TotalSeconds);
    }

    private static float SmoothStep(float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        return value * value * (3f - 2f * value);
    }

    public static double Mod(double value, double modulus)
    {
        return (value % modulus + modulus) % modulus;
    }

    /// <summary>
    /// Calculates light intensity as a function of time.
    /// </summary>
    public static double CalculateLightLevel(LightCycleComponent comp, float time)
    {
        var waveLength = MathF.Max(1, (float) comp.Duration.TotalSeconds);
        var crest = MathF.Max(0f, comp.MaxLightLevel);
        var shift = MathF.Max(0f, comp.MinLightLevel);
        return Math.Min(comp.ClipLight, CalculateCurve(time, waveLength, crest, shift, 6));
    }

    /// <summary>
    /// It is important to note that each color must have a different exponent, to modify how early or late one color should stand out in relation to another.
    /// This "simulates" what the atmosphere does and is what generates the effect of dawn and dusk.
    /// The blue component must be a cosine function with half period, so that its minimum is at dawn and dusk, generating the "warm" color corresponding to these periods.
    /// As you can see in the values, the maximums of the function serve more to define the curve behavior,
    /// they must be "clipped" so as not to distort the original color of the lighting. In practice, the maximum values, in fact, are the clip thresholds.
    /// </summary>
    public static Color CalculateColorLevel(LightCycleComponent comp, float time)
    {
        var waveLength = MathF.Max(1f, (float) comp.Duration.TotalSeconds);

        var red = MathF.Min(comp.ClipLevel.R,
            CalculateCurve(time,
                waveLength,
                MathF.Max(0f, comp.MaxLevel.R),
                MathF.Max(0f, comp.MinLevel.R),
                4f));

        var green = MathF.Min(comp.ClipLevel.G,
            CalculateCurve(time,
                waveLength,
                MathF.Max(0f, comp.MaxLevel.G),
                MathF.Max(0f, comp.MinLevel.G),
                10f));

        var blue = MathF.Min(comp.ClipLevel.B,
            CalculateCurve(time,
                waveLength / 2f,
                MathF.Max(0f, comp.MaxLevel.B),
                MathF.Max(0f, comp.MinLevel.B),
                2,
                waveLength / 4f));

        return new Color(red, green, blue);
    }

    /// <summary>
    /// Generates a sinusoidal curve as a function of x (time). The other parameters serve to adjust the behavior of the curve.
    /// </summary>
    /// <param name="x"> It corresponds to the independent variable of the function, which in the context of this algorithm is the current time. </param>
    /// <param name="waveLength"> It's the wavelength of the function, it can be said to be the total duration of the light cycle. </param>
    /// <param name="crest"> It's the maximum point of the function, where it will have its greatest value. </param>
    /// <param name="shift"> It's the vertical displacement of the function, in practice it corresponds to the minimum value of the function. </param>
    /// <param name="exponent"> It is the exponent of the sine, serves to "flatten" the function close to its minimum points and make it "steeper" close to its maximum. </param>
    /// <param name="phase"> It changes the phase of the wave, like a "horizontal shift". It is important to transform the sinusoidal function into cosine, when necessary. </param>
    /// <returns> The result of the function. </returns>
    public static float CalculateCurve(float x,
        float waveLength,
        float crest,
        float shift,
        float exponent,
        float phase = 0)
    {
        var sen = MathF.Pow(MathF.Sin((MathF.PI * (phase + x)) / waveLength), exponent);
        return (crest - shift) * sen + shift;
    }
}

/// <summary>
/// Raised when the offset on <see cref="LightCycleComponent"/> changes.
/// </summary>
[ByRefEvent]
public record struct LightCycleOffsetEvent(TimeSpan Offset)
{
    public readonly TimeSpan Offset = Offset;
}

public readonly record struct DayNightCalendarTime(int Day, TimeSpan TimeOfDay, bool IsDay);
