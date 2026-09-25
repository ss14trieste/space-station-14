using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Trieste.Bells.Components;
using Content.Shared._Trieste.Bells.Prototypes;
using Content.Shared.GameTicking.Events;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Trieste.Bells.Systems;

/// <summary>
///     Server-side entity system for the Bell component.
/// </summary>
public sealed partial class BellSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<ProtoId<BellTypePrototype>, List<BellPortEntry>> _ports = new();
    private readonly Dictionary<ProtoId<BellTypePrototype>, List<EntityUid>> _bells = new();

    private void SpawnBell(ProtoId<BellTypePrototype> bellType)
    {
        if (!ProtoMan.TryIndex(bellType, out var bellProto))
            return;

        if (_bells.TryGetValue(bellType, out var existing) && existing.Count > 0)
            return;

        _map.CreateMap(out var mapId);

        if (!_mapLoader.TryLoadGrid(mapId, bellProto.ShuttlePath, out var grid))
        {
            Log.Error($"Failed to load bell shuttle for {bellType} from {bellProto.ShuttlePath}");
            return;
        }

        var comp = new BellComponent { BellType = bellType };
        AddComp(grid.Value, comp);
    }

    [SubscribeLocalEvent]
    private void OnShuttleTag(EntityUid uid, BellPortComponent component, ref FTLTagEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.Tag = ProtoMan.Index(component.BellType).PriorityTag;
    }

    [SubscribeLocalEvent]
    private void OnPortStartup(EntityUid uid, BellPortComponent comp, ComponentStartup args)
    {
        if (Transform(uid).GridUid is not { } grid)
            return;

        _ports.GetOrNew(comp.BellType)
            .Add(new BellPortEntry(uid, grid, _station.GetOwningStation(grid), Loc.GetString(comp.PortName)));
    }

    [SubscribeLocalEvent]
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        foreach (var bellProto in ProtoMan.EnumeratePrototypes<BellTypePrototype>())
        {
            SpawnBell(bellProto.ID);
        }
    }

    [SubscribeLocalEvent]
    private void OnPortShutdown(EntityUid uid, BellPortComponent comp, ComponentShutdown args)
    {
        if (_ports.TryGetValue(comp.BellType, out var list))
            list.RemoveAll(e => e.Door == uid);
    }

    [SubscribeLocalEvent]
    private void OnStartup(EntityUid uid, BellComponent comp, ComponentStartup args)
    {
        comp.State = BellState.Unsummoned;
        comp.CurrentPort = null;
        comp.TransitStartTime = null;
        comp.TransitEndTime = null;

        _bells.GetOrNew(comp.BellType).Add(uid);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(EntityUid uid, BellComponent comp, ComponentShutdown args)
    {
        if (_bells.TryGetValue(comp.BellType, out var list))
            list.Remove(uid);
    }

    [SubscribeLocalEvent]
    private void OnFTLStarted(EntityUid uid, BellComponent comp, ref FTLStartedEvent args)
    {
        comp.State = BellState.InTransit;
        comp.CurrentPort = null;

        if (ProtoMan.TryIndex(comp.BellType, out var proto))
        {
            var duration = (proto.StartupTime ?? 0) + (proto.HyperspaceTime ?? 0);
            comp.TransitStartTime = _timing.CurTime;
            comp.TransitEndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);
        }
    }

    [SubscribeLocalEvent]
    private void OnFTLCompleted(EntityUid uid, BellComponent comp, ref FTLCompletedEvent args)
    {
        comp.State = BellState.Docked;
        comp.CurrentPort = TryFindDockedPort(uid, comp.BellType);
        comp.TransitStartTime = null;
        comp.TransitEndTime = null;

        if (comp.CurrentPort is null)
            Log.Warning($"Bell {ToPrettyString(uid)} completed its travel but couldn't find a matching port.");
    }

    private EntityUid? TryFindDockedPort(EntityUid bellUid, ProtoId<BellTypePrototype> bellType)
    {
        var dockQuery = GetEntityQuery<DockingComponent>();

        var enumerator = Transform(bellUid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!dockQuery.TryGetComponent(child, out var dock) || !dock.Docked || dock.DockedWith is not { } otherDock)
                continue;

            if (TryComp<BellPortComponent>(otherDock, out var port) && port.BellType == bellType)
                return otherDock;
        }

        return null;
    }

    public BellStatus GetBellStatus(ProtoId<BellTypePrototype> bellType)
    {
        if (!_bells.TryGetValue(bellType, out var list) || list.Count == 0)
            return new BellStatus(null, BellState.Unsummoned, false, null, null, null);

        var bellUid = list[0];
        var comp = Comp<BellComponent>(bellUid);
        var grid = comp.CurrentPort is { } port ? Transform(port).GridUid : Transform(bellUid).GridUid;

        return new BellStatus(bellUid, comp.State, comp.Locked, grid, comp.TransitStartTime, comp.TransitEndTime);
    }

    public bool TravelTo(ProtoId<BellTypePrototype> bellType, EntityUid destinationDoor)
    {
        var status = GetBellStatus(bellType);
        if (status.Bell is not { } bellUid)
            return false;

        if (status.Locked || status.State == BellState.InTransit)
            return false;

        if (!TryComp<BellPortComponent>(destinationDoor, out var destPort) || destPort.BellType != bellType)
            return false;

        if (!TryComp<DockingComponent>(destinationDoor, out _))
            return false;

        if (Transform(destinationDoor).GridUid is not { } destinationGrid)
            return false;

        if (!TryComp<ShuttleComponent>(bellUid, out var shuttleComp))
            return false;

        if (!ProtoMan.TryIndex(bellType, out var bellProto))
            return false;

        _shuttle.FTLToDock(
            bellUid,
            shuttleComp,
            destinationGrid,
            bellProto.StartupTime,
            bellProto.HyperspaceTime,
            bellProto.PriorityTag);

        return true;
    }

    public void SetLocked(ProtoId<BellTypePrototype> bellType, bool locked)
    {
        if (!_bells.TryGetValue(bellType, out var list))
            return;

        foreach (var bellUid in list)
        {
            if (TryComp<BellComponent>(bellUid, out var comp))
                comp.Locked = locked;
        }
    }

    public IReadOnlyList<BellPortEntry> GetDestinations(ProtoId<BellTypePrototype> bellType)
    {
        if (!_ports.TryGetValue(bellType, out var list))
            return Array.Empty<BellPortEntry>();

        return list
            .GroupBy(e => e.Grid)
            .Select(g => g.First())
            .ToList();
    }
}

public readonly record struct BellPortEntry(EntityUid Door, EntityUid Grid, EntityUid? Station, string Name);

public readonly record struct BellStatus(
    EntityUid? Bell,
    BellState State,
    bool Locked,
    EntityUid? CurrentGrid,
    TimeSpan? TransitStart,
    TimeSpan? TransitEnd);
