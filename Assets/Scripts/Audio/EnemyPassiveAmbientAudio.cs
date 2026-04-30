using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPassiveAmbientAudio : MonoBehaviour
{
    private const string DefaultResourcePath = "Audio/Sfx/mixkit-gasping-zombie-963";

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private AudioClip passiveAmbientClip;
    [SerializeField] private string passiveAmbientClipResourcePath = DefaultResourcePath;
    [SerializeField, Min(0.5f)] private float minAudibleDistance = 1f;
    [SerializeField, Min(1f)] private float maxAudibleDistance = 14f;
    [SerializeField, Range(0f, 1f)] private float passiveAmbientVolume = 0.05f;
    [SerializeField, Min(1f)] private float minInterval = 9f;
    [SerializeField, Min(1f)] private float maxInterval = 14f;
    [SerializeField, Range(0f, 1f)] private float followGlobalSfxMix = 1f;
    [SerializeField, Range(0.8f, 1.2f)] private float minPitch = 0.97f;
    [SerializeField, Range(0.8f, 1.2f)] private float maxPitch = 1.02f;
    [SerializeField] private bool enableDebugLogs;

    [SerializeField] private AudioSource ambientSource;
    private IEnemyPassiveAudioStateSource passiveStateSource;
    private float nextPlayTime;
    private float nextPlayerRefreshTime;
    private bool hasLoggedMissingClip;

    public void Initialize(WasdPlayerController player)
    {
        Configure(player);
        EnsureClipLoaded();
        ScheduleNextPlay(resetToSoon: true);
    }

    public void Configure(WasdPlayerController player)
    {
        playerController = player;
    }

    public void Configure(WasdPlayerController player, AudioSource audioSource)
    {
        playerController = player;
        ambientSource = audioSource;
        ConfigureAudioSource(ambientSource);
        EnsureClipLoaded();
    }

    private void Awake()
    {
        EnsureAudioSource();
        EnsureClipLoaded();
        CachePassiveStateSource();
        RefreshPlayerReference();
    }

    private void OnEnable()
    {
        CachePassiveStateSource();

        if (nextPlayTime <= 0f)
        {
            ScheduleNextPlay(resetToSoon: true);
        }
    }

    private void OnDisable()
    {
        if (ambientSource != null)
        {
            ambientSource.Stop();
        }
    }

    private void Update()
    {
        RefreshPlayerReference();

        if (!ShouldPlayAmbientNow())
        {
            if (ambientSource != null && ambientSource.isPlaying)
            {
                ambientSource.Stop();
            }

            return;
        }

        if (Time.time < nextPlayTime)
        {
            return;
        }

        EnsureClipLoaded();

        if (passiveAmbientClip == null)
        {
            if (enableDebugLogs && !hasLoggedMissingClip)
            {
                Debug.LogWarning($"{nameof(EnemyPassiveAmbientAudio)} could not resolve a passive ambient clip on '{gameObject.name}'.", this);
                hasLoggedMissingClip = true;
            }

            ScheduleNextPlay(resetToSoon: false);
            return;
        }

        float attenuation = CalculateAttenuation();
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);

        if (attenuation > 0.01f && sfxMixScalar > 0.001f)
        {
            EnsureAudioSource();
            ambientSource.pitch = Random.Range(minPitch, maxPitch);
            ambientSource.PlayOneShot(passiveAmbientClip, Mathf.Clamp01(passiveAmbientVolume * attenuation * sfxMixScalar));
        }

        ScheduleNextPlay(resetToSoon: false);
    }

    private void OnValidate()
    {
        minAudibleDistance = Mathf.Max(0.5f, minAudibleDistance);
        maxAudibleDistance = Mathf.Max(minAudibleDistance, maxAudibleDistance);
        passiveAmbientVolume = Mathf.Clamp01(passiveAmbientVolume);
        minInterval = Mathf.Max(1f, minInterval);
        maxInterval = Mathf.Max(minInterval, maxInterval);
        followGlobalSfxMix = Mathf.Clamp01(followGlobalSfxMix);
        minPitch = Mathf.Clamp(minPitch, 0.8f, 1.2f);
        maxPitch = Mathf.Clamp(maxPitch, minPitch, 1.2f);
    }

    private bool ShouldPlayAmbientNow()
    {
        CachePassiveStateSource();
        return passiveStateSource != null && passiveStateSource.ShouldPlayPassiveAmbientAudio;
    }

    private void EnsureAudioSource()
    {
        if (ambientSource != null)
        {
            return;
        }

        Transform existing = transform.Find("EnemyPassiveAmbientSource");
        GameObject sourceObject;

        if (existing != null)
        {
            sourceObject = existing.gameObject;
            ambientSource = sourceObject.GetComponent<AudioSource>();
        }
        else
        {
            sourceObject = new GameObject("EnemyPassiveAmbientSource");
            sourceObject.transform.SetParent(transform, false);
        }

        if (ambientSource == null)
        {
            ambientSource = sourceObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(ambientSource);
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
        if (passiveAmbientClip != null || string.IsNullOrWhiteSpace(passiveAmbientClipResourcePath))
        {
            return;
        }

        passiveAmbientClip = Resources.Load<AudioClip>(passiveAmbientClipResourcePath);
    }

    private void CachePassiveStateSource()
    {
        if (passiveStateSource != null)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        for (int index = 0; index < behaviours.Length; index++)
        {
            if (behaviours[index] is IEnemyPassiveAudioStateSource source)
            {
                passiveStateSource = source;
                return;
            }
        }
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
        return Mathf.Pow(softened, 1.25f);
    }

    private void ScheduleNextPlay(bool resetToSoon)
    {
        float delay = resetToSoon
            ? Random.Range(1.6f, 3.4f)
            : Random.Range(minInterval, maxInterval);
        nextPlayTime = Time.time + delay;
    }
}
