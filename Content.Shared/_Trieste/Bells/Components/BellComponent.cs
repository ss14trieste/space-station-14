using Content.Shared._Trieste.Bells.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Trieste.Bells.Components;

[RegisterComponent]
public sealed partial class BellComponent : Component
{
    [DataField(required: true)] public ProtoId<BellTypePrototype> BellType;

    [DataField] public bool Locked;

    public BellState State = BellState.Docked;
    public EntityUid? CurrentPort;
    public TimeSpan? TransitStartTime;
    public TimeSpan? TransitEndTime;
}

public enum BellState : byte
{
    Unsummoned,
    Docked,
    InTransit,
}
