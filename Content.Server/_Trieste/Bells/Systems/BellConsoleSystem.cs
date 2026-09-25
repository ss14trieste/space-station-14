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

    private void UpdateUi(EntityUid uid, BellConsoleComponent comp)
    {
        var status = _bell.GetBellStatus(comp.BellType);

        var destinations = _bell.GetDestinations(comp.BellType)
            .Select(e => new BellDestinationInfo(GetNetEntity(e.Door), e.Name))
            .ToList();

        var state = new BellConsoleBoundUserInterfaceState(
            destinations,
            status.State == BellState.Unsummoned,
            status.Locked,
            status.State == BellState.InTransit,
            null,
            status.TransitStart,
            status.TransitEnd);

        _ui.SetUiState(uid, BellConsoleUiKey.Key, state);
    }

    [SubscribeLocalEvent]
    private void OnTravelMessage(EntityUid uid, BellConsoleComponent comp, BellConsoleTravelMessage args)
    {
        Log.Debug($"[Bell] Server received travel message: door={args.Destination}, bellType={comp.BellType}");
        var doorUid = GetEntity(args.Destination);

        if (!_bell.TravelTo(comp.BellType, doorUid))
        {
            Log.Debug("[Bell] TravelTo returned false - check the five early-exit conditions.");
            return;
        }

        UpdateUi(uid, comp);
    }
}
