using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Baseline.RogueTown;

/// <summary>
/// Replaces connected RogueTown debug-tile regions with a basic bog after round setup has finished.
/// Work is deliberately spread over server ticks so even very large mapped regions do not stall the round.
/// </summary>
public sealed class RogueTownBogGenerationSystem : EntitySystem
{
    private const string DebugTilePrototype = "FloorRogueTownDebug";
    private const string GrassTilePrototype = "FloorRogueTownGrass";
    private const string DirtTilePrototype = "FloorRogueTownDirt";
    private const string YuckwaterTilePrototype = "FloorRogueTownYuckwater";
    private const string TreePrototype = "FloraTreeRogueTownStatic";
    private const string BushPrototype = "FloraRogueTownBush";

    private static readonly string[] GrassFloraPrototypes =
    {
        "FloraRogueTownGrass1",
        "FloraRogueTownGrass2",
        "FloraRogueTownGrass3",
    };

    // Exact per-region quotas keep small and enormous debug regions visually similar.
    private const float YuckwaterRatio = 0.08f;
    private const float DirtRatio = 0.15f;
    private const float TreeRatio = 0.08f;
    private const float BushRatio = 0.05f;
    private const float GrassFloraRatio = 0.10f;

    private static readonly Vector2i[] CardinalDirections =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    private readonly List<GridScanJob> _scanJobs = new();
    private readonly List<GeneratedRegion> _generatedRegions = new();
    private readonly Queue<(EntityUid GridUid, Vector2i Position)> _pendingDebugTiles = new();
    private readonly HashSet<(EntityUid GridUid, Vector2i Position)> _pendingDebugTileSet = new();
    private readonly Dictionary<EntityUid, Vector2i> _gridCenters = new();

    private GenerationPhase _phase = GenerationPhase.Idle;
    private bool _inRound;
    private bool _generationEnabled = true;
    private bool _initialScanStarted;
    private ResetMode _resetMode;
    private int _scanJobIndex;
    private RegionJob? _region;

    private int _resetRegionIndex;
    private int _resetFloraIndex;
    private int _resetTileIndex;

    private int _debugTileId;
    private int _grassTileId;
    private int _dirtTileId;
    private int _yuckwaterTileId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        _inRound = args.New == GameRunLevel.InRound;
        if (_generationEnabled && _inRound)
        {
            // PostGameMapLoad can fire before every round grid is present, and forced/sandbox starts may not
            // provide a lobby update tick at all. Always refresh the scan here so mapped debug tiles cannot be missed.
            _initialScanStarted = true;
            BeginScan();
        }
    }

    private void OnPostGameMapLoad(PostGameMapLoad args)
    {
        // Normal rounds preload their maps during the lobby. Starting here gives procgen the remaining lobby
        // ticks to work after deserialization, rather than beginning only after players enter the round.
        if (_initialScanStarted || _phase != GenerationPhase.Idle)
            return;

        _initialScanStarted = true;
        BeginScan();
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        // Initial map loading is handled by BeginScan. Runtime mapper additions are queued without rescanning.
        if (!_generationEnabled || !_inRound || _phase == GenerationPhase.ResetTiles)
            return;

        foreach (var change in args.Changes)
        {
            if (change.NewTile.TypeId != _debugTileId)
                continue;

            var pending = (args.Entity.Owner, change.GridIndices);
            if (_pendingDebugTileSet.Add(pending))
                _pendingDebugTiles.Enqueue(pending);
        }

        if (_phase == GenerationPhase.Idle && _pendingDebugTiles.Count > 0)
            _phase = GenerationPhase.Scan;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent args)
    {
        _phase = GenerationPhase.Idle;
        _scanJobs.Clear();
        _generatedRegions.Clear();
        _pendingDebugTiles.Clear();
        _pendingDebugTileSet.Clear();
        _gridCenters.Clear();
        _region = null;
        _inRound = false;
        _generationEnabled = true;
        _initialScanStarted = false;
    }

    /// <summary>
    /// Queues a rebuild of bog regions previously generated by this system, as well as any new debug tiles.
    /// </summary>
    public bool Regenerate()
    {
        if (_phase is GenerationPhase.ResetFlora or GenerationPhase.ResetTiles)
            return false;

        CacheTileIds();
        _generationEnabled = true;

        // This is an explicit admin override: cancel queued work and rebuild every tracked region.
        _scanJobs.Clear();
        _pendingDebugTiles.Clear();
        _pendingDebugTileSet.Clear();
        _region = null;
        _phase = GenerationPhase.Idle;
        _resetMode = ResetMode.Regenerate;

        if (_generatedRegions.Count == 0)
        {
            BeginScan();
            return true;
        }

        _resetRegionIndex = 0;
        _resetFloraIndex = 0;
        _resetTileIndex = 0;
        _phase = GenerationPhase.ResetFlora;
        return true;
    }

    /// <summary>
    /// Stops automatic generation and queues generated terrain to return to debug tiles.
    /// </summary>
    public bool Ungenerate()
    {
        if (_phase is GenerationPhase.ResetFlora or GenerationPhase.ResetTiles)
        {
            return false;
        }

        CacheTileIds();
        _generationEnabled = false;
        _scanJobs.Clear();
        _pendingDebugTiles.Clear();
        _pendingDebugTileSet.Clear();
        _region = null;

        _resetRegionIndex = 0;
        _resetFloraIndex = 0;
        _resetTileIndex = 0;
        _resetMode = ResetMode.Ungenerate;
        _phase = GenerationPhase.ResetFlora;
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Never intentionally claim more than half of one configured server tick.
        var budget = TimeSpan.FromTicks(Math.Max(1, _timing.TickPeriod.Ticks / 2));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        if (_phase == GenerationPhase.Idle)
            return;

        do
        {
            StepGeneration();
        }
        while (_phase != GenerationPhase.Idle && stopwatch.Elapsed < budget);
    }

    private void StepGeneration()
    {
        switch (_phase)
        {
            case GenerationPhase.ResetFlora:
                StepResetFlora();
                break;
            case GenerationPhase.ResetTiles:
                StepResetTiles();
                break;
            case GenerationPhase.Scan:
                StepScan();
                break;
            case GenerationPhase.DiscoverRegion:
                StepDiscoverRegion();
                break;
            case GenerationPhase.ShuffleRegion:
                StepShuffleRegion();
                break;
            case GenerationPhase.OrderRegion:
                StepOrderRegion();
                break;
            case GenerationPhase.FillRegion:
                StepFillRegion();
                break;
        }
    }

    private void BeginScan()
    {
        CacheTileIds();
        _scanJobs.Clear();
        _scanJobIndex = 0;
        _region = null;

        var query = EntityQueryEnumerator<MapGridComponent>();
        while (query.MoveNext(out var uid, out var grid))
        {
            _gridCenters.TryAdd(uid, GetGridCenter(grid));
            _scanJobs.Add(new GridScanJob(uid, grid, _maps.GetAllTilesEnumerator(uid, grid)));
        }

        _phase = _scanJobs.Count == 0 ? GenerationPhase.Idle : GenerationPhase.Scan;
    }

    private void CacheTileIds()
    {
        _debugTileId = _tileDefinitions[DebugTilePrototype].TileId;
        _grassTileId = _tileDefinitions[GrassTilePrototype].TileId;
        _dirtTileId = _tileDefinitions[DirtTilePrototype].TileId;
        _yuckwaterTileId = _tileDefinitions[YuckwaterTilePrototype].TileId;
    }

    private void StepScan()
    {
        if (_scanJobIndex >= _scanJobs.Count)
        {
            _scanJobs.Clear();
            if (!TryStartPendingRegion())
                _phase = GenerationPhase.Idle;
            return;
        }

        var scan = _scanJobs[_scanJobIndex];
        if (Deleted(scan.GridUid) || !TryComp<MapGridComponent>(scan.GridUid, out var currentGrid))
        {
            _scanJobIndex++;
            return;
        }

        scan.Grid = currentGrid;

        if (!scan.Tiles.MoveNext(out var tileRef))
        {
            _scanJobIndex++;
            return;
        }

        if (tileRef.Value.Tile.TypeId != _debugTileId)
            return;

        _region = new RegionJob(scan.GridUid,
            scan.Grid,
            tileRef.Value.GridIndices,
            _gridCenters.GetValueOrDefault(scan.GridUid, GetGridCenter(scan.Grid)));
        _phase = GenerationPhase.DiscoverRegion;
    }

    private void StepDiscoverRegion()
    {
        var region = _region!;
        if (region.Frontier.Count == 0)
        {
            // Release flood-fill bookkeeping before retaining the finished region for regeneration.
            region.Visited = null;
            region.ShuffleIndex = region.Tiles.Count - 1;
            if (region.ShuffleIndex > 0)
                _phase = GenerationPhase.ShuffleRegion;
            else
                PrepareRegionOrdering(region);
            return;
        }

        var position = region.Frontier.Dequeue();
        region.Tiles.Add(position);

        foreach (var direction in CardinalDirections)
        {
            var neighbor = position + direction;
            if (region.Visited!.Contains(neighbor) ||
                !_maps.TryGetTileRef(region.GridUid, region.Grid, neighbor, out var tile) ||
                tile.Tile.TypeId != _debugTileId)
            {
                continue;
            }

            region.Visited.Add(neighbor);
            region.Frontier.Enqueue(neighbor);
        }
    }

    private void StepShuffleRegion()
    {
        var region = _region!;
        if (region.ShuffleIndex <= 0)
        {
            PrepareRegionOrdering(region);
            return;
        }

        var swapIndex = _random.Next(region.ShuffleIndex + 1);
        (region.Tiles[swapIndex], region.Tiles[region.ShuffleIndex]) =
            (region.Tiles[region.ShuffleIndex], region.Tiles[swapIndex]);
        region.ShuffleIndex--;
    }

    private void PrepareRegionOrdering(RegionJob region)
    {
        var count = region.Tiles.Count;
        region.WaterCount = (int)MathF.Round(count * YuckwaterRatio);
        region.DirtCount = (int)MathF.Round(count * DirtRatio);
        var dryCount = count - region.WaterCount;
        var grassCount = dryCount - region.DirtCount;
        region.TreeCount = Math.Min(grassCount, (int)MathF.Round(dryCount * TreeRatio));
        region.BushCount = Math.Min(grassCount - region.TreeCount, (int)MathF.Round(dryCount * BushRatio));
        region.GrassFloraCount = Math.Min(grassCount - region.TreeCount - region.BushCount,
            (int)MathF.Round(dryCount * GrassFloraRatio));
        region.OrderIndex = 0;

        _generatedRegions.Add(new GeneratedRegion(region.GridUid, region.Tiles, region.SpawnedFlora));
        _phase = GenerationPhase.OrderRegion;
    }

    private void StepOrderRegion()
    {
        var region = _region!;
        if (region.OrderIndex >= region.Tiles.Count)
        {
            _phase = GenerationPhase.FillRegion;
            return;
        }

        // Terrain type is determined by the shuffled index, preserving exact randomized quotas. The priority
        // queue changes only placement order, making generation visibly spread from the region's center.
        var index = region.OrderIndex++;
        var position = region.Tiles[index];
        var tileId = index < region.WaterCount
            ? _yuckwaterTileId
            : index < region.WaterCount + region.DirtCount
                ? _dirtTileId
                : _grassTileId;
        string? spawnPrototype = null;
        var grassStart = region.WaterCount + region.DirtCount;
        // Flora is considered only after the water and dirt ranges, so it can only land on grass tiles.
        if (index >= grassStart)
        {
            var grassIndex = index - grassStart;
            var grassCount = region.Tiles.Count - grassStart;
            if (grassIndex >= grassCount - region.TreeCount)
                spawnPrototype = TreePrototype;
            else if (grassIndex >= grassCount - region.TreeCount - region.BushCount)
                spawnPrototype = BushPrototype;
            else if (grassIndex >= grassCount - region.TreeCount - region.BushCount - region.GrassFloraCount)
                spawnPrototype = _random.Pick(GrassFloraPrototypes);
        }
        var offset = position - region.Center;
        var distanceSquared = offset.X * offset.X + offset.Y * offset.Y;

        // A small random tie-breaker keeps equal-distance rings from looking mechanically ordered.
        region.OrderedTiles.Enqueue(new BogTile(position, tileId, spawnPrototype),
            distanceSquared + _random.NextFloat(0f, 0.25f));
    }

    private void StepFillRegion()
    {
        var region = _region!;
        if (!region.OrderedTiles.TryDequeue(out var bogTile, out _))
        {
            _region = null;
            _phase = GenerationPhase.Scan;
            return;
        }

        _maps.SetTile(region.GridUid, region.Grid, bogTile.Position, new Tile(bogTile.TileId));

        if (bogTile.SpawnPrototype is { } spawnPrototype)
        {
            var flora = Spawn(spawnPrototype, _maps.GridTileToLocal(region.GridUid, region.Grid, bogTile.Position));
            region.SpawnedFlora.Add(flora);
        }
    }

    private bool TryStartPendingRegion()
    {
        while (_pendingDebugTiles.TryDequeue(out var pending))
        {
            _pendingDebugTileSet.Remove(pending);
            if (!TryComp<MapGridComponent>(pending.GridUid, out var grid) ||
                !_maps.TryGetTileRef(pending.GridUid, grid, pending.Position, out var tile) ||
                tile.Tile.TypeId != _debugTileId)
            {
                continue;
            }

            if (!_gridCenters.TryGetValue(pending.GridUid, out var center))
            {
                center = GetGridCenter(grid);
                _gridCenters[pending.GridUid] = center;
            }

            _region = new RegionJob(pending.GridUid, grid, pending.Position, center);
            _phase = GenerationPhase.DiscoverRegion;
            return true;
        }

        return false;
    }

    private static Vector2i GetGridCenter(MapGridComponent grid)
    {
        var center = grid.LocalAABB.Center / grid.TileSize;
        return new Vector2i((int)MathF.Floor(center.X), (int)MathF.Floor(center.Y));
    }

    private void StepResetFlora()
    {
        if (_resetRegionIndex >= _generatedRegions.Count)
        {
            _resetRegionIndex = 0;
            _resetTileIndex = 0;
            _phase = GenerationPhase.ResetTiles;
            return;
        }

        var region = _generatedRegions[_resetRegionIndex];
        if (_resetFloraIndex >= region.SpawnedFlora.Count)
        {
            _resetRegionIndex++;
            _resetFloraIndex = 0;
            return;
        }

        var flora = region.SpawnedFlora[_resetFloraIndex++];
        if (!Deleted(flora))
            QueueDel(flora);
    }

    private void StepResetTiles()
    {
        if (_resetRegionIndex >= _generatedRegions.Count)
        {
            _generatedRegions.Clear();
            if (_resetMode == ResetMode.Ungenerate)
                _phase = GenerationPhase.Idle;
            else
                BeginScan();
            return;
        }

        var region = _generatedRegions[_resetRegionIndex];
        if (_resetTileIndex >= region.Tiles.Count)
        {
            _resetRegionIndex++;
            _resetTileIndex = 0;
            return;
        }

        if (!TryComp<MapGridComponent>(region.GridUid, out var grid))
        {
            _resetRegionIndex++;
            _resetTileIndex = 0;
            return;
        }

        _maps.SetTile(region.GridUid, grid, region.Tiles[_resetTileIndex++], new Tile(_debugTileId));
    }

    private enum GenerationPhase : byte
    {
        Idle,
        ResetFlora,
        ResetTiles,
        Scan,
        DiscoverRegion,
        ShuffleRegion,
        OrderRegion,
        FillRegion,
    }

    private enum ResetMode : byte
    {
        Regenerate,
        Ungenerate,
    }

    private sealed class GridScanJob(
        EntityUid gridUid,
        MapGridComponent grid,
        GridTileEnumerator tiles)
    {
        public readonly EntityUid GridUid = gridUid;
        public MapGridComponent Grid = grid;
        public GridTileEnumerator Tiles = tiles;
    }

    private sealed class RegionJob
    {
        public readonly EntityUid GridUid;
        public readonly MapGridComponent Grid;
        public readonly List<Vector2i> Tiles = new();
        public readonly Queue<Vector2i> Frontier = new();
        public HashSet<Vector2i>? Visited = new();
        public readonly List<EntityUid> SpawnedFlora = new();
        public readonly PriorityQueue<BogTile, float> OrderedTiles = new();

        public int ShuffleIndex;
        public int OrderIndex;
        public int WaterCount;
        public int DirtCount;
        public int TreeCount;
        public int BushCount;
        public int GrassFloraCount;
        public readonly Vector2i Center;

        public RegionJob(EntityUid gridUid, MapGridComponent grid, Vector2i seed, Vector2i center)
        {
            GridUid = gridUid;
            Grid = grid;
            Center = center;
            Frontier.Enqueue(seed);
            Visited!.Add(seed);
        }
    }

    private readonly record struct BogTile(Vector2i Position, int TileId, string? SpawnPrototype);

    private sealed record GeneratedRegion(
        EntityUid GridUid,
        List<Vector2i> Tiles,
        List<EntityUid> SpawnedFlora);
}

/// <summary>
/// Rebuilds all bog areas created from RogueTown debug tiles.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class RogueTownRegenerateCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "regenerate";
    public string Description => "Regenerates RogueTown bog procgen regions and processes any new debug tiles.";
    public string Help => "Usage: regenerate";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var generator = _entityManager.System<RogueTownBogGenerationSystem>();
        if (!generator.Regenerate())
        {
            shell.WriteError("RogueTown bog generation is already running. Try again after it finishes.");
            return;
        }

        shell.WriteLine("Queued RogueTown bog regeneration. It will remain throttled while it runs.");
    }
}

/// <summary>
/// Starts RogueTown generation without requiring mappers to remember the regenerate alias.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class RogueTownGenerateCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "generate";
    public string Description => "Generates RogueTown bog terrain from mapped debug tiles.";
    public string Help => "Usage: generate";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var generator = _entityManager.System<RogueTownBogGenerationSystem>();
        if (!generator.Regenerate())
        {
            shell.WriteError("RogueTown bog generation is already being reset. Try again after it finishes.");
            return;
        }

        shell.WriteLine("Queued RogueTown bog generation from mapped debug tiles.");
    }
}

/// <summary>
/// Returns RogueTown procgen terrain to debug tiles and pauses automatic processing for mapping.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class RogueTownUngenerateCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "ungenerate";
    public string Description => "Returns RogueTown bog procgen to debug tiles and pauses automatic generation.";
    public string Help => "Usage: ungenerate";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var generator = _entityManager.System<RogueTownBogGenerationSystem>();
        if (!generator.Ungenerate())
        {
            shell.WriteError("RogueTown bog generation is already being reset. Try again after it finishes.");
            return;
        }

        shell.WriteLine("Queued RogueTown bog rollback to debug tiles. Generation is paused until regenerate is run.");
    }
}
