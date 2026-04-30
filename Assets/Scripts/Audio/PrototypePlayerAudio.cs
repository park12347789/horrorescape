using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class PrototypePlayerAudio : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float movementThreshold = 0.08f;
    [SerializeField, Min(0.08f)] private float walkStepInterval = 0.42f;
    [SerializeField, Min(0.05f)] private float sprintStepInterval = 0.19f;
    [SerializeField, Min(0.1f)] private float walkReferenceSpeed = 4.2f;
    [SerializeField, Min(0.1f)] private float sprintReferenceSpeed = 6.8f;
    [SerializeField, Range(0f, 1f)] private float walkStepStrengthFloor = 0.44f;
    [SerializeField, Range(0f, 1f)] private float sprintStepStrengthFloor = 0.88f;
    [SerializeField, Range(0.5f, 1.5f)] private float cadenceAtLowSpeed = 1.08f;
    [SerializeField, Range(0.5f, 1.5f)] private float cadenceAtHighSpeed = 0.72f;

    private Rigidbody2D body;
    private WasdPlayerController playerController;
    private NoiseEmitter noiseEmitter;
    private float nextStepTime;

    public event System.Action<bool, float> FootstepPlayed;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerController = GetComponent<WasdPlayerController>();
        noiseEmitter = GetComponent<NoiseEmitter>();
    }

    private void OnEnable()
    {
        nextStepTime = Time.time + 0.08f;
    }

    private void Update()
    {
        if (body == null || playerController == null)
        {
            return;
        }

        float speed = body.linearVelocity.magnitude;

        if (speed < movementThreshold)
        {
            nextStepTime = Time.time + 0.04f;
            return;
        }

        if (Time.time < nextStepTime)
        {
            return;
        }

        bool sprinting = playerController.IsSprinting;
        float speedReference = sprinting ? sprintReferenceSpeed : walkReferenceSpeed;
        float normalizedSpeed = Mathf.InverseLerp(movementThreshold, speedReference, speed);
        float cadenceScale = Mathf.Lerp(cadenceAtLowSpeed, cadenceAtHighSpeed, normalizedSpeed);
        float interval = (sprinting ? sprintStepInterval : walkStepInterval) * cadenceScale;
        float strengthFloor = sprinting ? sprintStepStrengthFloor : walkStepStrengthFloor;
        float strength = Mathf.Lerp(strengthFloor, 1f, normalizedSpeed);

        if (!PrototypeAudioManager.TryPlayFootstep(sprinting, strength))
        {
            return;
        }

        noiseEmitter ??= GetComponent<NoiseEmitter>();
        noiseEmitter?.EmitMovementNoiseFromFootstep(sprinting, strength);
        FootstepPlayed?.Invoke(sprinting, strength);
        nextStepTime = Time.time + interval;
    }

    private void OnValidate()
    {
        movementThreshold = Mathf.Max(0.01f, movementThreshold);
        walkStepInterval = Mathf.Max(0.08f, walkStepInterval);
        sprintStepInterval = Mathf.Max(0.05f, sprintStepInterval);
        walkReferenceSpeed = Mathf.Max(0.1f, walkReferenceSpeed);
        sprintReferenceSpeed = Mathf.Max(0.1f, sprintReferenceSpeed);
        walkStepStrengthFloor = Mathf.Clamp01(walkStepStrengthFloor);
        sprintStepStrengthFloor = Mathf.Clamp01(sprintStepStrengthFloor);
        cadenceAtLowSpeed = Mathf.Clamp(cadenceAtLowSpeed, 0.5f, 1.5f);
        cadenceAtHighSpeed = Mathf.Clamp(cadenceAtHighSpeed, 0.5f, 1.5f);
    }
}
