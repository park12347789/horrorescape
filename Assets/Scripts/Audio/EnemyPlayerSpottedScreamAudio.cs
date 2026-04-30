using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPlayerSpottedScreamAudio : MonoBehaviour
{
    private const string DefaultResourcePath = "Audio/Sfx/alex_jauk-zombie-screaming-207590";
    private static AudioClip sharedDefaultPlayerSpottedClip;

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private AudioClip playerSpottedClip;
    [SerializeField] private string playerSpottedClipResourcePath = DefaultResourcePath;
    [SerializeField, Min(0.5f)] private float minAudibleDistance = 1f;
    [SerializeField, Min(1f)] private float maxAudibleDistance = 14f;
    [SerializeField, Range(0f, 1f)] private float spottedVolume = 0.32f;
    [SerializeField, Min(0f)] private float clipStartTime = 0f;
    [SerializeField, Min(0.05f)] private float maxPlaybackDuration = 1f;
    [SerializeField, Min(0.05f)] private float spottedCooldown = 0.6f;
    [SerializeField, Range(0f, 1f)] private float followGlobalSfxMix = 1f;
    [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.99f;
    [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.04f;
    [SerializeField] private bool enableDebugLogs;

    [SerializeField] private AudioSource spottedSource;
    private IEnemyPlayerSpotSource playerSpotSource;
    private float nextAllowedPlayTime;
    private float playbackStopTime = float.NegativeInfinity;
    private bool hasLoggedMissingClip;

    public void Initialize(WasdPlayerController player)
    {
        Configure(player);
        EnsureClipLoaded();
        SubscribeToPlayerSpotSource();
    }

    public void Configure(WasdPlayerController player)
    {
        playerController = player;
    }

    public void Configure(WasdPlayerController player, AudioSource audioSource)
    {
        playerController = player;
        spottedSource = audioSource;
        ConfigureAudioSource(spottedSource);
        EnsureClipLoaded();
    }

    private void Awake()
    {
        EnsureAudioSource();
        EnsureClipLoaded();
        CachePlayerSpotSource();
        RefreshPlayerReference();
    }

    private void OnEnable()
    {
        SubscribeToPlayerSpotSource();
    }

    private void OnDisable()
    {
        UnsubscribeFromPlayerSpotSource();

        if (spottedSource != null)
        {
            spottedSource.Stop();
        }

        playbackStopTime = float.NegativeInfinity;
    }

    private void OnValidate()
    {
        minAudibleDistance = Mathf.Max(0.5f, minAudibleDistance);
        maxAudibleDistance = Mathf.Max(minAudibleDistance, maxAudibleDistance);
        spottedVolume = Mathf.Clamp01(spottedVolume);
        clipStartTime = Mathf.Max(0f, clipStartTime);
        maxPlaybackDuration = Mathf.Max(0.05f, maxPlaybackDuration);
        spottedCooldown = Mathf.Max(0.05f, spottedCooldown);
        followGlobalSfxMix = Mathf.Clamp01(followGlobalSfxMix);
        minPitch = Mathf.Clamp(minPitch, 0.8f, 1.2f);
        maxPitch = Mathf.Clamp(maxPitch, minPitch, 1.2f);
    }

    private void Update()
    {
        if (spottedSource == null || !spottedSource.isPlaying || Time.time < playbackStopTime)
        {
            return;
        }

        spottedSource.Stop();
        playbackStopTime = float.NegativeInfinity;
    }

    private void SubscribeToPlayerSpotSource()
    {
        UnsubscribeFromPlayerSpotSource();
        CachePlayerSpotSource();

        if (playerSpotSource != null)
        {
            playerSpotSource.PlayerSpotted += HandlePlayerSpotted;
        }
    }

    private void UnsubscribeFromPlayerSpotSource()
    {
        if (playerSpotSource != null)
        {
            playerSpotSource.PlayerSpotted -= HandlePlayerSpotted;
        }
    }

    private void HandlePlayerSpotted()
    {
        if (Time.time < nextAllowedPlayTime)
        {
            return;
        }

        EnsureClipLoaded();

        if (playerSpottedClip == null)
        {
            if (enableDebugLogs && !hasLoggedMissingClip)
            {
                Debug.LogWarning($"{nameof(EnemyPlayerSpottedScreamAudio)} could not resolve a spotted scream clip on '{gameObject.name}'.", this);
                hasLoggedMissingClip = true;
            }

            return;
        }

        EnsureAudioSource();
        RefreshPlayerReference();

        float attenuation = CalculateAttenuation();
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);

        if (attenuation <= 0.01f || sfxMixScalar <= 0.001f)
        {
            return;
        }

        float pitch = Random.Range(minPitch, maxPitch);
        float clampedClipStartTime = Mathf.Clamp(clipStartTime, 0f, Mathf.Max(0f, playerSpottedClip.length - 0.05f));
        float remainingClipDuration = Mathf.Max(0.05f, playerSpottedClip.length - clampedClipStartTime);

        spottedSource.Stop();
        spottedSource.clip = playerSpottedClip;
        spottedSource.volume = Mathf.Clamp01(spottedVolume * attenuation * sfxMixScalar);
        spottedSource.pitch = pitch;
        spottedSource.time = clampedClipStartTime;
        spottedSource.Play();
        playbackStopTime = Time.time + CalculatePlaybackStopDelay(remainingClipDuration, maxPlaybackDuration, pitch);
        nextAllowedPlayTime = Time.time + spottedCooldown;
    }

    private static float CalculatePlaybackStopDelay(float remainingClipDuration, float maxPlaybackDuration, float pitch)
    {
        float remainingPlaybackDelay = Mathf.Max(0.05f, remainingClipDuration) / Mathf.Max(0.01f, pitch);
        return Mathf.Min(remainingPlaybackDelay, Mathf.Max(0.05f, maxPlaybackDuration));
    }

    private void EnsureAudioSource()
    {
        if (spottedSource != null)
        {
            return;
        }

        Transform existing = transform.Find("EnemyPlayerSpottedScreamSource");
        GameObject sourceObject;

        if (existing != null)
        {
            sourceObject = existing.gameObject;
            spottedSource = sourceObject.GetComponent<AudioSource>();
        }
        else
        {
            sourceObject = new GameObject("EnemyPlayerSpottedScreamSource");
            sourceObject.transform.SetParent(transform, false);
        }

        if (spottedSource == null)
        {
            spottedSource = sourceObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(spottedSource);
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.reverbZoneMix = 0f;
    }

    private void EnsureClipLoaded()
    {
        if (playerSpottedClip != null || string.IsNullOrWhiteSpace(playerSpottedClipResourcePath))
        {
            return;
        }

        if (string.Equals(playerSpottedClipResourcePath, DefaultResourcePath, System.StringComparison.Ordinal))
        {
            sharedDefaultPlayerSpottedClip ??= Resources.Load<AudioClip>(DefaultResourcePath);
            playerSpottedClip = sharedDefaultPlayerSpottedClip;
            return;
        }

        playerSpottedClip = Resources.Load<AudioClip>(playerSpottedClipResourcePath);
    }

    private void RefreshPlayerReference()
    {
        if (playerController != null)
        {
            return;
        }

        playerController = AudioScenePlayerReferenceResolver.ResolveCurrentOrSceneFallback(playerController, gameObject);
    }

    private void CachePlayerSpotSource()
    {
        if (playerSpotSource != null)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int index = 0; index < behaviours.Length; index++)
        {
            if (behaviours[index] is IEnemyPlayerSpotSource source)
            {
                playerSpotSource = source;
                return;
            }
        }
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
        return Mathf.Pow(softened, 1.2f);
    }
}
