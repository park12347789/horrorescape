using System;

public interface IStaminaSource
{
    event Action Changed;

    float CurrentStamina { get; }
    float MaxStamina { get; }
    float NormalizedStamina { get; }
    float StaminaNormalized { get; }
    bool CanSprint { get; }
    bool IsDraining { get; }
    bool IsRecovering { get; }
    bool IsExhausted { get; }
    bool ShouldShowStaminaHud { get; }
}
