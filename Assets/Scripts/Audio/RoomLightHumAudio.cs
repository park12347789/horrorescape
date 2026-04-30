using System;

using UnityEngine;

[DefaultExecutionOrder(550)]
[DisallowMultipleComponent]
public sealed class RoomLightHumAudio : MonoBehaviour
{
    private const string DefaultResourcePath = "Audio/Sfx/FlickeringFluorescentLightHum_kentspublicdomain_CC0";
    private const string DefaultAudioSourceName = "HumSource";

    [SerializeField] private AudioSource humSource;
    [SerializeField] private FluorescentFlicker2D flicker;
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private AudioClip humClip;
    [SerializeField] private string humClipResourcePath = DefaultResourcePath;
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.065f;
    [SerializeField, Min(0.5f)] private float minAudibleDistance = 1.2f;
    [SerializeField, Min(1f)] private float maxAudibleDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float followGlobalSfxMix = 1f;
    [SerializeField, Range(0f, 1f)] private float flickerVolumeResponse = 0.45f;
    [SerializeField, Range(0f, 0.4f)] private float flickerPitchResponse = 0.08f;
    [SerializeField] private bool useInstanceSeed = true;
    [SerializeField] private int seedOffset;
    [SerializeField] private Vector2 pitchRange = new(0.97f, 1.03f);
    [SerializeField] private Vector2 volumeRandomRange = new(0.92f, 1.08f);
    [SerializeField] private bool randomizeStartTime = true;

    private System.Random random;
    private bool runtimeInitialized;
    private float instancePitch = 1f;
    private float instanceVolumeScalar = 1f;
    private float startTimeNormalized;
    private float currentVolume;
    private float nextPlayerRefreshTime;

    public string HumClipResourcePath => humClipResourcePath;

    public void Configure(WasdPlayerController player)
    {
        playerController = player;
    }

    private void Awake()
    {
        EnsureReferences();
        EnsureClipLoaded();
        InitializeRuntimeState();
        SyncLoopPlayback(forceRestart: false);
    }

    private void OnEnable()
    {
        EnsureReferences();
        EnsureClipLoaded();
        InitializeRuntimeState();
        SyncLoopPlayback(forceRestart: false);
    }

    private void OnDisable()
    {
        if (humSource != null)
        {
            humSource.Stop();
        }
    }

    private void OnValidate()
    {
        minAudibleDistance = Mathf.Max(0.5f, minAudibleDistance);
        maxAudibleDistance = Mathf.Max(minAudibleDistance, maxAudibleDistance);
        baseVolume = Mathf.Clamp01(baseVolume);
        followGlobalSfxMix = Mathf.Clamp01(followGlobalSfxMix);
        flickerVolumeResponse = Mathf.Clamp01(flickerVolumeResponse);
        flickerPitchResponse = Mathf.Clamp(flickerPitchResponse, 0f, 0.4f);
        pitchRange.x = Mathf.Clamp(pitchRange.x, 0.8f, 1.2f);
        pitchRange.y = Mathf.Clamp(pitchRange.y, pitchRange.x, 1.2f);
        volumeRandomRange.x = Mathf.Clamp(volumeRandomRange.x, 0.1f, 2f);
        volumeRandomRange.y = Mathf.Clamp(volumeRandomRange.y, volumeRandomRange.x, 2f);
    }

    private void LateUpdate()
    {
        if (humSource == null || humClip == null)
        {
            return;
        }

        RefreshPlayerReference();

        float attenuation = CalculateAttenuation();
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);
        float flickerScalar = GetFlickerVolumeScalar();
        float targetVolume = Mathf.Clamp01(baseVolume * instanceVolumeScalar * attenuation * sfxMixScalar * flickerScalar);
        float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
        currentVolume = Mathf.Lerp(currentVolume, targetVolume, t);
        humSource.volume = currentVolume;
        humSource.pitch = instancePitch * GetFlickerPitchScalar();
    }

    private void EnsureReferences()
    {
        if (flicker == null)
        {
            flicker = GetComponent<FluorescentFlicker2D>();
        }

        if (humSource != null)
        {
            return;
        }

        Transform existing = transform.Find(DefaultAudioSourceName);
        GameObject sourceObject;

        if (existing != null)
        {
            sourceObject = existing.gameObject;
            humSource = sourceObject.GetComponent<AudioSource>();
        }
        else
        {
            sourceObject = new GameObject(DefaultAudioSourceName);
            sourceObject.transform.SetParent(transform, false);
        }

        if (humSource == null)
        {
            humSource = sourceObject.AddComponent<AudioSource>();
        }

        humSource.playOnAwake = false;
        humSource.loop = true;
        humSource.spatialBlend = 0f;
        humSource.dopplerLevel = 0f;
        humSource.rolloffMode = AudioRolloffMode.Linear;
        humSource.reverbZoneMix = 0f;
    }

    private void EnsureClipLoaded()
    {
        if (humClip != null || string.IsNullOrWhiteSpace(humClipResourcePath))
        {
            return;
        }

        humClip = Resources.Load<AudioClip>(humClipResourcePath);
    }

    private void InitializeRuntimeState()
    {
        if (runtimeInitialized)
        {
            return;
        }

        int runtimeSeed = useInstanceSeed
            ? CombineSeed(seedOffset, gameObject.GetInstanceID())
            : seedOffset;
        random = new System.Random(runtimeSeed);
        instancePitch = NextFloat(pitchRange);
        instanceVolumeScalar = NextFloat(volumeRandomRange);
        startTimeNormalized = randomizeStartTime ? NextFloat(0f, 1f) : 0f;
        runtimeInitialized = true;
    }

    private void SyncLoopPlayback(bool forceRestart)
    {
        if (humSource == null || humClip == null)
        {
            return;
        }

        bool clipChanged = humSource.clip != humClip;

        if (clipChanged)
        {
            humSource.clip = humClip;
        }

        if (!forceRestart && humSource.isPlaying && !clipChanged)
        {
            return;
        }

        humSource.Stop();

        if (randomizeStartTime && humClip.length > 0.05f)
        {
            humSource.time = Mathf.Clamp(startTimeNormalized * humClip.length, 0f, humClip.length - 0.05f);
        }
        else
        {
            humSource.time = 0f;
        }

        humSource.Play();
    }

    private void RefreshPlayerReference()
    {
        if (playerController != null || Time.time < nextPlayerRefreshTime)
        {
            return;
        }

        playerController = AudioScenePlayerReferenceResolver.ResolveCurrentOrSceneFallback(playerController, gameObject);
        nextPlayerRefreshTime = Time.time + 1f;
    }

    private float CalculateAttenuation()
    {
        if (playerController == null)
        {
            return 1f;
        }

        Vector2 toPlayer = (Vector2)transform.position - (Vector2)playerController.transform.position;
        float sqrDistance = toPlayer.sqrMagnitude;
        float minAudibleDistanceSqr = minAudibleDistance * minAudibleDistance;

        if (sqrDistance <= minAudibleDistanceSqr)
        {
            return 1f;
        }

        float maxAudibleDistanceSqr = maxAudibleDistance * maxAudibleDistance;

        if (sqrDistance >= maxAudibleDistanceSqr)
        {
            return 0f;
        }

        float distance = Mathf.Sqrt(sqrDistance);
        float normalized = Mathf.InverseLerp(maxAudibleDistance, minAudibleDistance, distance);
        float softened = Mathf.SmoothStep(0f, 1f, normalized);
        return Mathf.Pow(softened, 1.15f);
    }

    private float GetFlickerVolumeScalar()
    {
        if (flicker == null)
        {
            return 1f;
        }

        float multiplier = Mathf.Clamp(flicker.CurrentLightMultiplier, 0.12f, 1f);
        return Mathf.Lerp(1f, multiplier, flickerVolumeResponse);
    }

    private float GetFlickerPitchScalar()
    {
        if (flicker == null)
        {
            return 1f;
        }

        float multiplier = Mathf.Clamp(flicker.CurrentLightMultiplier, 0.2f, 1f);
        float pitchScalar = Mathf.Lerp(1f - flickerPitchResponse, 1f, multiplier);
        return Mathf.Clamp(pitchScalar, 0.8f, 1.2f);
    }

    private float NextFloat(Vector2 range)
    {
        return NextFloat(range.x, range.y);
    }

    private float NextFloat(float min, float max)
    {
        if (random == null)
        {
            return min;
        }

        double t = random.NextDouble();
        return Mathf.Lerp(min, max, (float)t);
    }

    private static int CombineSeed(int left, int right)
    {
        unchecked
        {
            return (left * 397) ^ right;
        }
    }
}
