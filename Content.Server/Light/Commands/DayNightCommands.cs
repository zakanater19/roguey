using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Light.EntitySystems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Light.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed class DayCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "day";
    public string Description => "Switches the day/night cycle to full daylight.";
    public string Help => "day";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!DayNightCommandUtility.Validate(shell, args, _entities.System<GameTicker>()))
            return;

        var changed = _entities.System<LightCycleSystem>().SetPhase(true);
        shell.WriteLine(changed > 0 ? "Switched to day." : "No day/night enabled maps were found.");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class NightCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "night";
    public string Description => "Switches the day/night cycle to 22:12, when full night begins.";
    public string Help => "night";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!DayNightCommandUtility.Validate(shell, args, _entities.System<GameTicker>()))
            return;

        var changed = _entities.System<LightCycleSystem>().SetPhase(false);
        shell.WriteLine(changed > 0 ? "Switched to full night (22:12)." : "No day/night enabled maps were found.");
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed class TimeSpeedCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "timespeed";
    public string Description => "Sets the day/night clock speed from 1 to 20.";
    public string Help => "timespeed <1-20>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_entities.System<GameTicker>().RunLevel != GameRunLevel.InRound)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-while-round-is-active"));
            return;
        }

        if (args.Length != 1 || !int.TryParse(args[0], out var timeScale) || timeScale is < 1 or > 20)
        {
            shell.WriteError(Help);
            return;
        }

        var changed = _entities.System<LightCycleSystem>().SetTimeScale(timeScale);
        shell.WriteLine(changed > 0
            ? $"Day/night clock speed set to {timeScale}x."
            : "No day/night enabled maps were found.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHint("<1-20>")
            : CompletionResult.Empty;
    }
}

internal static class DayNightCommandUtility
{
    public static bool Validate(IConsoleShell shell, string[] args, GameTicker ticker)
    {
        if (ticker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-while-round-is-active"));
            return false;
        }

        if (args.Length == 0)
            return true;

        shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
        return false;
    }
}
