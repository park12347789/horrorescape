using UnityEngine;

[CreateAssetMenu(fileName = "StaminaConfig", menuName = "HorrorStealth/Player/Stamina Config")]
public sealed class StaminaConfig : ScriptableObject
{
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float sprintDrainPerSecond = 18f;
    [SerializeField, Min(0f)] private float recoverPerSecond = 14f;
    [SerializeField, Min(0f)] private float recoverDelay = 1f;
    [SerializeField, Min(0f)] private float exhaustedLockDuration = 1.6f;
    [SerializeField, Min(0f)] private float exhaustedRecoverDelay = 0.35f;
    [SerializeField, Min(0f)] private float minimumSprintStartStamina = 4f;

    public float MaxStamina => Mathf.Max(1f, maxStamina);
    public float SprintDrainPerSecond => Mathf.Max(0f, sprintDrainPerSecond);
    public float RecoverPerSecond => Mathf.Max(0f, recoverPerSecond);
    public float RecoverDelay => Mathf.Max(0f, recoverDelay);
    public float ExhaustedLockDuration => Mathf.Max(0f, exhaustedLockDuration);
    public float ExhaustedRecoverDelay => Mathf.Max(0f, exhaustedRecoverDelay);
    public float MinimumSprintStartStamina => Mathf.Max(0f, minimumSprintStartStamina);

    private void OnValidate()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        sprintDrainPerSecond = Mathf.Max(0f, sprintDrainPerSecond);
        recoverPerSecond = Mathf.Max(0f, recoverPerSecond);
        recoverDelay = Mathf.Max(0f, recoverDelay);
        exhaustedLockDuration = Mathf.Max(0f, exhaustedLockDuration);
        exhaustedRecoverDelay = Mathf.Max(0f, exhaustedRecoverDelay);
        minimumSprintStartStamina = Mathf.Max(0f, minimumSprintStartStamina);
    }
}
