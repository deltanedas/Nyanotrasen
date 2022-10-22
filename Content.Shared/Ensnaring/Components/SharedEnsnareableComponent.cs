using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Ensnaring.Components;
/// <summary>
/// Use this on an entity that you would like to be ensnared by anything that has the <see cref="SharedEnsnaringComponent"/>
/// </summary>
[NetworkedComponent]
public abstract class SharedEnsnareableComponent : Component
{
    /// <summary>
    /// How much should this slow down the entities walk?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("walkSpeed")]
    public float WalkSpeed = 1.0f;

    /// <summary>
    /// How much should this slow down the entities sprint?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("sprintSpeed")]
    public float SprintSpeed = 1.0f;

    /// <summary>
    /// Is this entity currently ensnared?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isEnsnared")]
    public bool IsEnsnared;
}

[Serializable, NetSerializable]
public sealed class EnsnareableComponentState : ComponentState
{
    public readonly bool IsEnsnared;

    public EnsnareableComponentState(bool isEnsnared)
    {
        IsEnsnared = isEnsnared;
    }
}

public sealed class EnsnaredChangedEvent : EntityEventArgs
{
    public readonly bool IsEnsnared;

    public EnsnaredChangedEvent(bool isEnsnared)
    {
        IsEnsnared = isEnsnared;
    }
}
