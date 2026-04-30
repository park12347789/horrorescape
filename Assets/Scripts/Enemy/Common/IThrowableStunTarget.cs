using UnityEngine;

public interface IThrowableStunTarget
{
    bool CanBeStunnedByThrowable { get; }
    Vector3 ThrowableStunAimPoint { get; }
    float ThrowableStunHitRadius { get; }
    bool TryApplyThrowableStun(float duration);
}
