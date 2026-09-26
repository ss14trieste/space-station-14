using System.Linq;
using Content.Shared._Trieste.Bells.Components;
using Content.Shared._Trieste.Bells.Misc;
using Robust.Server.GameObjects;

namespace Content.Server._Trieste.Bells.Systems;

public sealed partial class BellConsoleSystem : EntitySystem
{
    [Dependency] private BellSystem _bell = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnOpened(EntityUid uid, BellConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    public void UpdateUi(EntityUid uid, BellConsoleComponent comp)
    {
        var status = _bell.GetBellStatus(comp.BellType);

        var destinations = _bell.GetDestinations(comp.BellType)
            .Select(e => new BellDestinationInfo(GetNetEntity(e.Door), e.Name))
            .ToList();

        var currentLocationName = string.Empty;
        if (status.CurrentPort is { } port && TryComp<BellPortComponent>(port, out var portComp))
            currentLocationName = Loc.GetString(portComp.PortName);

        Log.Warning($"Current location is {currentLocationName}");

        var state = new BellConsoleBoundUserInterfaceState(
            destinations,
            status.Locked,
            status.FtlState,
            currentLocationName,
            status.PhaseStart,
            status.PhaseEnd);

        _ui.SetUiState(uid, BellConsoleUiKey.Key, state);
    }

    [SubscribeLocalEvent]
    private void OnTravelMessage(EntityUid uid, BellConsoleComponent comp, BellConsoleTravelMessage args)
    {
        var doorUid = GetEntity(args.Destination);

        if (!_bell.TravelTo(comp.BellType, doorUid))
            return;

        _bell.RefreshConsolesFor(comp.BellType);
    }
}
