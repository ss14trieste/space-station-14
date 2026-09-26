using Content.Shared._Trieste.Bells.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Trieste.Bells.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BellConsoleComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BellTypePrototype> BellType;
}
