using System.Collections.Generic;
using System.Globalization;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._Baseline.Mapping;

/// <summary>
/// Keeps force-saved live station maps reloadable as fresh round maps.
/// </summary>
public sealed class CleanMapSaveSystem : EntitySystem
{
    private EntityQuery<MapComponent> _mapQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mapQuery = GetEntityQuery<MapComponent>();
        SubscribeLocalEvent<AfterSerializationEvent>(OnAfterSerialization);
    }

    private void OnAfterSerialization(AfterSerializationEvent ev)
    {
        if (ev.Category != FileCategory.Map || !IncludesInitializedMap(ev.Entities))
            return;

        SanitizeInitializedMapSave(ev.Node);
    }

    private bool IncludesInitializedMap(HashSet<EntityUid> entities)
    {
        foreach (var entity in entities)
        {
            if (_mapQuery.TryComp(entity, out var map) && map.MapInitialized)
                return true;
        }

        return false;
    }

    private static void SanitizeInitializedMapSave(MappingDataNode data)
    {
        var removedUids = new HashSet<string>();

        if (!data.TryGet<SequenceDataNode>("entities", out var prototypeSections))
            return;

        for (var i = prototypeSections.Count - 1; i >= 0; i--)
        {
            if (prototypeSections[i] is not MappingDataNode prototypeSection ||
                !prototypeSection.TryGet<ValueDataNode>("proto", out var protoNode) ||
                !prototypeSection.TryGet<SequenceDataNode>("entities", out var entities))
            {
                continue;
            }

            if (protoNode.Value == "StandardNanotrasenStation")
            {
                CollectYamlUids(entities, removedUids);
                prototypeSections.RemoveAt(i);
                continue;
            }

            foreach (var entityNode in entities)
            {
                if (entityNode is not MappingDataNode entity)
                    continue;

                entity.Remove("mapInit");
                entity.Remove("paused");

                if (entity.TryGet<SequenceDataNode>("components", out var components))
                    SanitizeInitializedMapComponents(components);
            }
        }

        RemoveYamlIds(data, "nullspace", removedUids);
        RemoveYamlIds(data, "orphans", removedUids);

        if (data.TryGet<MappingDataNode>("meta", out var meta))
            meta["entityCount"] = new ValueDataNode(CountSerializedEntities(prototypeSections).ToString(CultureInfo.InvariantCulture));
    }

    private static void SanitizeInitializedMapComponents(SequenceDataNode components)
    {
        for (var i = components.Count - 1; i >= 0; i--)
        {
            if (components[i] is not MappingDataNode component ||
                !component.TryGet<ValueDataNode>("type", out var type))
            {
                continue;
            }

            switch (type.Value)
            {
                case "Map":
                    component.Remove("mapInitialized");
                    component["mapPaused"] = new ValueDataNode("True");
                    break;
                case "FTLDestination":
                case "StationMember":
                case "TileHistory":
                case "NavMap":
                case "GridAtmosphere":
                case "GasTileOverlay":
                    components.RemoveAt(i);
                    break;
                case "SpreaderGrid":
                    component.Remove("updateAccumulator");
                    break;
            }
        }
    }

    private static void CollectYamlUids(SequenceDataNode entities, HashSet<string> uids)
    {
        foreach (var entityNode in entities)
        {
            if (entityNode is MappingDataNode entity &&
                entity.TryGet<ValueDataNode>("uid", out var uid))
            {
                uids.Add(uid.Value);
            }
        }
    }

    private static void RemoveYamlIds(MappingDataNode data, string key, HashSet<string> removedUids)
    {
        if (removedUids.Count == 0 || !data.TryGet<SequenceDataNode>(key, out var ids))
            return;

        for (var i = ids.Count - 1; i >= 0; i--)
        {
            if (ids[i] is ValueDataNode uid && removedUids.Contains(uid.Value))
                ids.RemoveAt(i);
        }
    }

    private static int CountSerializedEntities(SequenceDataNode prototypeSections)
    {
        var count = 0;
        foreach (var node in prototypeSections)
        {
            if (node is MappingDataNode section &&
                section.TryGet<SequenceDataNode>("entities", out var entities))
            {
                count += entities.Count;
            }
        }

        return count;
    }
}
