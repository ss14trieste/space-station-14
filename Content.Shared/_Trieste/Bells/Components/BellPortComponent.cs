using Content.Shared._Trieste.Bells.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Trieste.Bells.Components;

[RegisterComponent]
public sealed partial class BellPortComponent : Component
{
    [DataField(required: true)]
    public ProtoId<BellTypePrototype> BellType;

    /// <summary>
    ///     Display name for the UI list.
    /// </summary>
    [DataField]
    public string PortName = string.Empty;
}
