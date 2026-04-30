/*
 * File Role:
 * Emits noise events from player actions such as sprinting and interacting.
 *
 * Runtime Use:
 * Translates local player behavior into NoiseSystem events that enemies can react to.
 *
 * Study Notes:
 * This file is a good example of how moment-to-moment actions feed higher-level AI systems.
 */

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class NoiseEmitter : MonoBehaviour
{
    [Header("Movement Noise (Recognition Relative)")]
    [SerializeField, Min(0.1f)] private float referenceRecognitionRadius = 4.6f;
    [SerializeField, Range(0.1f, 1.2f)] private float walkNoiseRadiusRatio = 0.29f;
    [SerializeField, Range(0.1f, 1.2f)] private float sprintNoiseRadiusRatio = 0.86f;
    [SerializeField, Min(0f)] private float walkNoiseRecognitionMargin = 0.6f;
    [SerializeField, Min(0f)] private float sprintNoiseRecognitionMargin = 0.15f;
    [SerializeField, Range(0f, 1f)] private float minimumMovementStrengthScale = 0.56f;
    [SerializeField, Min(0f)] private float minimumMovementNoiseRadius = 0.55f;
    [SerializeField, Min(0.05f)] private float walkNoiseInterval = 0.42f;
    [SerializeField, Min(0.05f)] private float sprintNoiseInterval = 0.26f;
    [SerializeField, Min(0f)] private float movementThreshold = 0.15f;
    [SerializeField] private bool preferFootstepDrivenNoise = true;
    [Header("Action Noise")]
    [SerializeField, Min(0f)] private float interactNoiseRadius = 2.4f;
    [SerializeField] private NoiseSystem noiseSystem;

    private Rigidbody2D body;
    private WasdPlayerController playerController;
    private PrototypePlayerAudio playerAudio;
    private INoiseEventBus noiseEventBus;
    private int emitterInstanceId;
    private float nextMovementNoiseTime;
    private float lastFootstepNoiseTime = float.NegativeInfinity;

    private const float FootstepFallbackGraceSeconds = 0.65f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        playerController = GetComponent<WasdPlayerController>();
        playerAudio = GetComponent<PrototypePlayerAudio>();
        emitterInstanceId = gameObject.GetInstanceID();
    }

    private void Update()
    {
        if (ShouldDeferToFootstepNoise())
        {
            return;
        }

        EmitMovementNoiseFromVelocity();
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public void EmitInteractNoise()
    {
        ResolveNoiseEventBus()?.TryEmitNoise(
            transform.position,
            interactNoiseRadius,
            NoiseSourceType.Interact,
            emitterInstanceId,
            NoiseEmitterAffiliation.Player,
            allowDebugPulse: false);
    }

    public void EmitMovementNoiseFromFootstep(bool sprinting, float strength)
    {
        lastFootstepNoiseTime = Time.time;
        TryEmitMovementNoise(sprinting, strength);
    }

    private bool ShouldDeferToFootstepNoise()
    {
        if (!preferFootstepDrivenNoise)
        {
            return false;
        }

        playerAudio ??= GetComponent<PrototypePlayerAudio>();

        if (playerAudio == null || !playerAudio.isActiveAndEnabled)
        {
            return false;
        }

        return Time.time - lastFootstepNoiseTime <= FootstepFallbackGraceSeconds;
    }

    private void EmitMovementNoiseFromVelocity()
    {
        if (body == null || playerController == null)
        {
            return;
        }

        float velocityMagnitude = body.linearVelocity.magnitude;

        if (velocityMagnitude < movementThreshold)
        {
            nextMovementNoiseTime = Time.time;
            return;
        }

        bool isSprinting = playerController.IsSprinting;
        float speedCeiling = isSprinting ? 6.8f : 4.2f;
        float normalizedSpeed = Mathf.InverseLerp(movementThreshold, speedCeiling, velocityMagnitude);
        float strength = Mathf.Lerp(minimumMovementStrengthScale, 1f, normalizedSpeed);

        TryEmitMovementNoise(isSprinting, strength);
    }

    private void TryEmitMovementNoise(bool sprinting, float strength)
    {
        if (Time.time < nextMovementNoiseTime)
        {
            return;
        }

        float radius = ComputeMovementNoiseRadius(sprinting, strength);
        NoiseSourceType sourceType = sprinting ? NoiseSourceType.Sprint : NoiseSourceType.Walk;
        // The emitted radius is treated as the authoritative gameplay hearing range so
        // debug pulses and enemy hearing stay on the same footprint.
        ResolveNoiseEventBus()?.TryEmitNoise(
            transform.position,
            radius,
            sourceType,
            emitterInstanceId,
            NoiseEmitterAffiliation.Player,
            allowDebugPulse: false);
        nextMovementNoiseTime = Time.time + (sprinting ? sprintNoiseInterval : walkNoiseInterval);
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private float ComputeMovementNoiseRadius(bool sprinting, float strength)
    {
        float recognitionRadius = Mathf.Max(0.1f, referenceRecognitionRadius);
        float radiusRatio = sprinting ? sprintNoiseRadiusRatio : walkNoiseRadiusRatio;
        float margin = sprinting ? sprintNoiseRecognitionMargin : walkNoiseRecognitionMargin;
        float recognitionCappedRadius = Mathf.Max(minimumMovementNoiseRadius, recognitionRadius - margin);

        if (!sprinting)
        {
            // Walking should present one readable radius that stays slightly below the
            // reference recognition ring instead of wobbling with minor speed changes.
            return Mathf.Min(recognitionRadius * radiusRatio, recognitionCappedRadius);
        }

        float clampedStrength = Mathf.Clamp01(strength);
        float strengthScale = Mathf.Lerp(minimumMovementStrengthScale, 1f, clampedStrength);
        float computedRadius = recognitionRadius * radiusRatio * strengthScale;
        computedRadius = Mathf.Min(computedRadius, recognitionCappedRadius);

        return Mathf.Max(minimumMovementNoiseRadius, computedRadius);
    }

    private void OnValidate()
    {
        referenceRecognitionRadius = Mathf.Max(0.1f, referenceRecognitionRadius);
        walkNoiseRadiusRatio = Mathf.Clamp(walkNoiseRadiusRatio, 0.1f, 1.2f);
        sprintNoiseRadiusRatio = Mathf.Clamp(sprintNoiseRadiusRatio, 0.1f, 1.2f);
        walkNoiseRecognitionMargin = Mathf.Max(0f, walkNoiseRecognitionMargin);
        sprintNoiseRecognitionMargin = Mathf.Max(0f, sprintNoiseRecognitionMargin);
        minimumMovementStrengthScale = Mathf.Clamp01(minimumMovementStrengthScale);
        minimumMovementNoiseRadius = Mathf.Max(0f, minimumMovementNoiseRadius);
        walkNoiseInterval = Mathf.Max(0.05f, walkNoiseInterval);
        sprintNoiseInterval = Mathf.Max(0.05f, sprintNoiseInterval);
        movementThreshold = Mathf.Max(0f, movementThreshold);
        interactNoiseRadius = Mathf.Max(0f, interactNoiseRadius);
    }
}

