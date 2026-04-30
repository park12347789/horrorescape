using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class PrototypeAudioManager : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const int SfxPoolSize = 6;
    private const float MaxLegacyAmbienceVolume = 0.05f;
    private const float StartupTraceDurationSeconds = 6f;
    private const float AmbienceStartupOffsetSeconds = 2.1f;
    private const float ElevatorRideNoiseVolume = 0.34f;
    private const float ElevatorArrivalNoiseDelaySeconds = 0.42f;
    public const float ElevatorArrivalDoorOpenDelaySeconds = 0.52f;
    private const string LobbyMusicResourcePath = "Audio/Music/CreepyAmbientLoopV2_epb9000_CC0";
    private const string GameplayMusicResourcePath = "Audio/Music/EmptyCity_yd_CC0";
    private const string NoiseTrapSfxResourcePath = "Audio/Sfx/GlassTrap_ShardStep_Mixkit172";
    private const string BottleShatterSfxResourcePath = "Audio/Sfx/GlassShatter3_GregSurr_CC0";
    private const string DefaultDoorMechanismSfxResourcePath = "Audio/Sfx/mechanical1_BMacZero_CC0";
    private const string WalkFootstepSfxResourcePath = "Audio/Sfx/FootstepsStoneSneaker_xkeril_CC0";
    private const string PickupSfxResourcePath = "Audio/Sfx/qubodup-hover2";
    private const string BatteryReplaceSfxResourcePath = "Audio/Sfx/battery_replace_remote_cover_3s";
    private const string DeniedSfxResourcePath = "Audio/Sfx/error";
    private const string ElevatorTransitionSfxResourcePath = "Audio/Sfx/elevator_sound_pixabay";
    private const string ElevatorEscapeSfxResourcePath = "Audio/Sfx/old_elevator_door";
    private const string ElevatorDingSfxResourcePath = "Audio/Sfx/freesound_community-elevator-dingwav-14913";

    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.92f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.05f;
    [SerializeField, Range(1f, 6f)] private float externalMusicVolumeMultiplier = 1f;
    [SerializeField] private bool enableStartupTrace;
    [Header("Footstep Mix")]
    [SerializeField, Range(0f, 1f)] private float walkFootstepBaseVolume = 0.304f;
    [SerializeField, Range(0f, 1f)] private float sprintFootstepBaseVolume = 0.448f;
    [SerializeField] private Vector2 walkFootstepPitchRange = new(0.93f, 1.02f);
    [SerializeField] private Vector2 sprintFootstepPitchRange = new(0.98f, 1.08f);
    [Header("Trap Mix")]
    [SerializeField, Range(0f, 1f)] private float noiseTrapBaseVolume = 0.58f;
    [Header("Ambience Floor Blend")]
    [SerializeField, Range(0f, 1f)] private float lowFloorAmbienceMultiplier = 0.34f;
    [SerializeField, Range(0f, 1f)] private float highFloorAmbienceMultiplier = 0.48f;
    [SerializeField, Range(0f, 1f)] private float escapedAmbienceMultiplier = 0.05f;

    private static PrototypeAudioManager instance;
    private readonly List<AudioSource> sfxSources = new();
    private PrototypeSoundBank soundBank;
    private AudioSource ambienceSource;
    private AudioSource doorSource;
    private AudioSource elevatorRideSource;
    private Coroutine ambienceFadeRoutine;
    private Coroutine floorTransitionRoutine;
    private Coroutine delayedAmbienceRoutine;
    private int roundRobinIndex;
    private float startupTraceUntilRealtime;
    private Scene cachedRuntimeScene;
    [System.Obsolete("Legacy compatibility bridge. Prefer TryGetCachedInstance, EnsureExists, or explicit scene audio APIs.", false)]
    public static PrototypeAudioManager Instance => instance;
    public float MasterVolume => masterVolume;
    public float SfxVolume => sfxVolume;
    public float AmbienceVolume => ambienceVolume;

    public static bool TryGetCachedInstance(out PrototypeAudioManager audioManager)
    {
        audioManager = instance;
        return audioManager != null;
    }

    public static float GetRuntimeSfxMixScalar()
    {
        if (instance == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(instance.masterVolume * instance.sfxVolume);
    }

    public static float GetRuntimeAmbienceMixScalar()
    {
        if (instance == null)
        {
            return 1f;
        }

        return Mathf.Clamp01(instance.masterVolume * instance.ambienceVolume);
    }

    public static PrototypeAudioManager EnsureExists()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject audioObject = new("PrototypeAudioManager");
        return audioObject.AddComponent<PrototypeAudioManager>();
    }

    public static void TrySetFloorAmbience(int floorNumber, bool escaped)
    {
        instance?.TraceStartup($"TrySetFloorAmbience request floor={floorNumber} escaped={escaped}");
        instance?.SetFloorAmbience(floorNumber, escaped);
    }

    public static void TrySetFloorAmbienceDelayed(int floorNumber, bool escaped, float delaySeconds)
    {
        instance?.TraceStartup($"TrySetFloorAmbienceDelayed request floor={floorNumber} escaped={escaped} delay={delaySeconds:0.00}");
        instance?.SetFloorAmbienceDelayed(floorNumber, escaped, delaySeconds);
    }

    public static void TryPlayPickup()
    {
        instance?.PlayPickup();
    }

    public static void TryPlayBatteryReplace()
    {
        instance?.PlayBatteryReplace();
    }

    public static void TryPlayDenied()
    {
        instance?.PlayDenied();
    }

    public static void TryPlayDoorOpen()
    {
        instance?.PlayDoorOpen();
    }

    public static void TryPlayDoorClose()
    {
        instance?.PlayDoorClose();
    }

    public static void TryPlayNoiseTrap(float intensity = 1f)
    {
        instance?.PlayNoiseTrap(intensity);
    }

    public static bool TryPlayFootstep(bool sprinting, float strength)
    {
        if (instance == null)
        {
            return false;
        }

        instance.PlayFootstep(sprinting, strength);
        return true;
    }

    public static void TryPlayBottleShatter(bool hitEnemy = false)
    {
        instance?.PlayBottleShatter(hitEnemy);
    }

    public static void TryPlayFloorTransition(int floorNumber)
    {
        instance?.PlayFloorTransition(floorNumber);
    }

    public static void TryPlayElevatorArrival(int floorNumber)
    {
        instance?.PlayElevatorArrival(floorNumber);
    }

    public static void TryPlayElevatorDing()
    {
        instance?.PlayElevatorDing();
    }

    public static void TryPlayElevatorRideNoise()
    {
        instance?.PlayElevatorRideNoise();
    }

    public static void TryStopElevatorRideNoise()
    {
        instance?.StopElevatorRideNoise();
    }

    public static void TryPlayFinalEscape()
    {
        instance?.PlayFinalEscape();
    }

    public static void TryPrewarmFinalExitAudio()
    {
        instance?.PrewarmFinalExitAudio();
    }

    [System.Obsolete("Legacy compatibility bridge. Prefer TryApplySceneAmbienceForScene with an explicit owning scene.", false)]
    public static void TryApplySceneAmbienceForActiveScene()
    {
        EnsureExists().ApplySceneAmbienceForLoadedRuntimeScene(immediate: true);
    }

    public static void TryApplySceneAmbienceForScene(Scene scene)
    {
        EnsureExists().ApplySceneAmbienceForScene(scene, immediate: true);
    }

    public void SetRuntimeMixVolumes(float newMasterVolume, float newSfxVolume, float newAmbienceVolume)
    {
        float clampedMaster = Mathf.Clamp01(newMasterVolume);
        float clampedSfx = Mathf.Clamp01(newSfxVolume);
        float clampedAmbience = Mathf.Clamp01(newAmbienceVolume);
        bool ambienceRelevantValueChanged =
            !Mathf.Approximately(masterVolume, clampedMaster) ||
            !Mathf.Approximately(ambienceVolume, clampedAmbience);

        masterVolume = clampedMaster;
        sfxVolume = clampedSfx;
        ambienceVolume = clampedAmbience;

        if (!Application.isPlaying || !ambienceRelevantValueChanged)
        {
            return;
        }

        ApplySceneAmbienceForCachedScene(immediate: true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapRuntimeAudio()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PrototypeAudioManager manager = EnsureExists();
        manager.ApplySceneAmbienceForLoadedRuntimeScene(immediate: true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null, false);
        }

        DontDestroyOnLoad(gameObject);
        ApplyPersistedRuntimeOptions();
        startupTraceUntilRealtime = Time.realtimeSinceStartup + StartupTraceDurationSeconds;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TraceStartup($"Awake ambienceVolume={ambienceVolume:0.000}");
    }

    private void Start()
    {
        if (!ApplySceneAmbienceForCachedScene(immediate: true))
        {
            ApplySceneAmbienceForLoadedRuntimeScene(immediate: true);
        }
    }

    private void OnDestroy()
    {
        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
        }

        if (delayedAmbienceRoutine != null)
        {
            StopCoroutine(delayedAmbienceRoutine);
        }

        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }

    public void PlayFootstep(bool sprinting, float strength)
    {
        EnsureRuntimeAudioReady();

        if (soundBank == null)
        {
            return;
        }

        AudioClip[] candidates = sprinting ? soundBank.sprintFootsteps : soundBank.walkFootsteps;

        if (candidates == null || candidates.Length == 0)
        {
            return;
        }

        AudioClip clip = candidates[Random.Range(0, candidates.Length)];
        float volume = sprinting ? sprintFootstepBaseVolume : walkFootstepBaseVolume;
        Vector2 pitchRange = sprinting ? sprintFootstepPitchRange : walkFootstepPitchRange;
        float pitch = Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));
        PlayClip(clip, volume * Mathf.Clamp(strength, 0.2f, 1.15f), pitch);
    }

    public void PlayPickup()
    {
        EnsureRuntimeAudioReady();
        PlayClip(soundBank?.pickupClip, 0.84f, Random.Range(1.01f, 1.06f));
    }

    public void PlayBatteryReplace()
    {
        EnsureRuntimeAudioReady();
        PlayClip(soundBank?.batteryReplaceClip ?? soundBank?.pickupClip, 0.74f, Random.Range(0.98f, 1.02f));
    }

    public void PlayDenied()
    {
        EnsureRuntimeAudioReady();
        PlayClip(soundBank?.deniedClip, 0.58f, Random.Range(0.92f, 0.98f));
    }

    public void PlayDoorOpen()
    {
        EnsureRuntimeAudioReady();
        PlayDoorClip(soundBank?.doorOpenClip, 0.86f, Random.Range(0.96f, 1.02f));
    }

    public void PlayDoorClose()
    {
        EnsureRuntimeAudioReady();
        PlayDoorClip(soundBank?.doorCloseClip, 0.64f, Random.Range(0.92f, 0.97f));
    }

    public void PlayNoiseTrap(float intensity = 1f)
    {
        EnsureRuntimeAudioReady();
        float clampedIntensity = Mathf.Clamp(intensity, 0.55f, 1.35f);
        PlayClip(soundBank?.noiseTrapClip, noiseTrapBaseVolume * clampedIntensity, Random.Range(0.94f, 1.05f));
    }

    public void PlayBottleShatter(bool hitEnemy = false)
    {
        EnsureRuntimeAudioReady();
        AudioClip clip = soundBank?.bottleShatterClip ?? soundBank?.bottleImpactClip;
        PlayClip(clip, 0.5f, Random.Range(0.94f, 1.03f));
    }

    public void PlayFloorTransition(int floorNumber)
    {
        EnsureRuntimeAudioReady();

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (floorTransitionRoutine != null)
        {
            StopCoroutine(floorTransitionRoutine);
        }

        floorTransitionRoutine = StartCoroutine(PlayFloorTransitionRoutine(floorNumber));
    }

    public void PlayElevatorArrival(int floorNumber)
    {
        EnsureRuntimeAudioReady();

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (floorTransitionRoutine != null)
        {
            StopCoroutine(floorTransitionRoutine);
        }

        floorTransitionRoutine = StartCoroutine(PlayElevatorArrivalRoutine(floorNumber));
    }

    public void PlayElevatorDing()
    {
        EnsureRuntimeAudioReady();
        PlayClip(soundBank?.arrivalChimeClip, 0.48f, 1f);
    }

    public void PlayElevatorRideNoise()
    {
        EnsureRuntimeAudioReady();

        if (elevatorRideSource == null || soundBank?.floorDropClip == null)
        {
            return;
        }

        elevatorRideSource.loop = true;
        elevatorRideSource.clip = soundBank.floorDropClip;
        elevatorRideSource.pitch = 1f;
        elevatorRideSource.volume = Mathf.Clamp01(ElevatorRideNoiseVolume * sfxVolume * masterVolume);

        if (!elevatorRideSource.isPlaying)
        {
            TraceStartup($"ElevatorRideSource.Play() clip={soundBank.floorDropClip.name} volume={elevatorRideSource.volume:0.000}");
            elevatorRideSource.Play();
        }
    }

    public void StopElevatorRideNoise()
    {
        if (elevatorRideSource == null)
        {
            return;
        }

        elevatorRideSource.Stop();
        elevatorRideSource.clip = null;
    }

    public void PlayFinalEscape()
    {
        EnsureRuntimeAudioReady();

        if (floorTransitionRoutine != null)
        {
            StopCoroutine(floorTransitionRoutine);
            floorTransitionRoutine = null;
        }

        PlayClip(soundBank?.escapeClip, 0.62f, 1f);
        SetFloorAmbience(1, true);
    }

    private void PrewarmFinalExitAudio()
    {
        EnsureFinalExitAudioReady();
    }

    private IEnumerator PlayFloorTransitionRoutine(int floorNumber)
    {
        TraceStartup($"PlayFloorTransitionRoutine start floor={floorNumber}");

        if (soundBank?.stairBreachClip != null)
        {
            PlayClip(soundBank.stairBreachClip, 0.44f, Random.Range(0.95f, 0.99f));
            yield return new WaitForSeconds(0.16f);
        }

        PlayClip(soundBank?.floorDropClip, 0.52f, Random.Range(0.98f, 1.02f));
        SetFloorAmbience(floorNumber, false);
        floorTransitionRoutine = null;
    }

    private IEnumerator PlayElevatorArrivalRoutine(int floorNumber)
    {
        TraceStartup($"PlayElevatorArrivalRoutine start floor={floorNumber}");

        PlayClip(soundBank?.arrivalChimeClip, 0.48f, 1f);
        yield return new WaitForSecondsRealtime(ElevatorArrivalNoiseDelaySeconds);

        if (elevatorRideSource == null || !elevatorRideSource.isPlaying)
        {
            PlayClip(soundBank?.floorDropClip, 0.48f, Random.Range(0.98f, 1.02f));
        }

        SetFloorAmbience(floorNumber, false);
        floorTransitionRoutine = null;
    }

    private void EnsureSources()
    {
        if (ambienceSource == null)
        {
            ambienceSource = CreateSource("AmbienceSource", true);
            TraceStartup("Created AmbienceSource");
        }

        if (doorSource == null)
        {
            doorSource = CreateSource("DoorSource", false);
            TraceStartup("Created DoorSource");
        }

        if (elevatorRideSource == null)
        {
            elevatorRideSource = CreateSource("ElevatorRideSource", true);
            TraceStartup("Created ElevatorRideSource");
        }

        EnsureSharedSfxSources();
    }

    private void EnsureRuntimeAudioReady()
    {
        EnsureSoundBank();
        EnsureSources();
    }

    private void EnsureFinalExitAudioReady()
    {
        EnsureSoundBank();
        EnsureSharedSfxSources();
    }

    private void EnsureSoundBank()
    {
        if (soundBank != null)
        {
            return;
        }

        soundBank = PrototypeProceduralAudio.CreateSoundBank(SampleRate);
        TraceStartup("Created procedural sound bank");
    }

    private void EnsureSharedSfxSources()
    {
        while (sfxSources.Count < SfxPoolSize)
        {
            sfxSources.Add(CreateSource($"SfxSource_{sfxSources.Count + 1}", false));
        }
    }

    private AudioSource CreateSource(string name, bool looping)
    {
        GameObject sourceObject = new(name);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = looping;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.reverbZoneMix = 0f;
        source.volume = 1f;
        return source;
    }

    private void EnsureAmbienceLoopStarted()
    {
        EnsureAmbienceLoopStarted(soundBank?.ambienceLoop, AmbienceStartupOffsetSeconds);
    }

    private void EnsureAmbienceLoopStarted(AudioClip clip, float startupOffsetSeconds)
    {
        EnsureRuntimeAudioReady();

        if (ambienceSource == null || clip == null)
        {
            return;
        }

        bool clipChanged = ambienceSource.clip != clip;
        ambienceSource.clip = clip;
        ambienceSource.loop = true;

        if (clipChanged && ambienceSource.isPlaying)
        {
            ambienceSource.Stop();
        }

        if (!ambienceSource.isPlaying)
        {
            ambienceSource.volume = 0f;
            ambienceSource.time = Mathf.Clamp(
                startupOffsetSeconds,
                0f,
                Mathf.Max(0f, clip.length - 0.05f));
            TraceStartup($"AmbienceSource.Play() clip={clip.name}");
            ambienceSource.Play();
        }
    }

    private void SetFloorAmbience(int floorNumber, bool escaped)
    {
        EnsureRuntimeAudioReady();

        if (ambienceSource == null)
        {
            return;
        }

        float floorBlend = Mathf.InverseLerp(1f, 5f, floorNumber);
        float targetPitch = escaped
            ? 0.82f
            : Mathf.Lerp(1.02f, 0.88f, floorBlend);
        float floorAmbienceMultiplier = escaped
            ? escapedAmbienceMultiplier
            : Mathf.Lerp(lowFloorAmbienceMultiplier, highFloorAmbienceMultiplier, floorBlend);
        float floorPresence = escaped ? 0.32f : Mathf.Lerp(0.92f, 1.08f, floorBlend);
        AudioClip ambienceClip = ResolveGameplayAmbienceClip();
        float targetVolume = masterVolume * ambienceVolume * floorAmbienceMultiplier * floorPresence;

        if (ambienceClip != null)
        {
            targetVolume *= externalMusicVolumeMultiplier;
        }

        TraceStartup($"SetFloorAmbience floor={floorNumber} escaped={escaped} targetVolume={targetVolume:0.000} targetPitch={targetPitch:0.000}");
        EnsureAmbienceLoopStarted(ambienceClip != null ? ambienceClip : soundBank?.ambienceLoop, AmbienceStartupOffsetSeconds);
        ambienceSource.pitch = targetPitch;
        StartAmbienceFade(Mathf.Clamp01(targetVolume), escaped ? 0.9f : 1.8f);
    }

    private void SetFloorAmbienceDelayed(int floorNumber, bool escaped, float delaySeconds)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (delayedAmbienceRoutine != null)
        {
            StopCoroutine(delayedAmbienceRoutine);
        }

        TraceStartup($"Queue delayed ambience floor={floorNumber} escaped={escaped} delay={delaySeconds:0.00}");
        delayedAmbienceRoutine = StartCoroutine(SetFloorAmbienceDelayedRoutine(floorNumber, escaped, Mathf.Max(0f, delaySeconds)));
    }

    private void StartAmbienceFade(float targetVolume, float duration)
    {
        if (ambienceSource == null)
        {
            return;
        }

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
        }

        if (!isActiveAndEnabled || duration <= 0.01f)
        {
            ambienceSource.volume = targetVolume;
            ambienceFadeRoutine = null;
            return;
        }

        ambienceFadeRoutine = StartCoroutine(FadeAmbienceVolumeRoutine(targetVolume, duration));
    }

    private IEnumerator FadeAmbienceVolumeRoutine(float targetVolume, float duration)
    {
        float startingVolume = ambienceSource != null ? ambienceSource.volume : 0f;
        float elapsed = 0f;

        while (ambienceSource != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - normalized, 3f);
            ambienceSource.volume = Mathf.Lerp(startingVolume, targetVolume, eased);
            yield return null;
        }

        if (ambienceSource != null)
        {
            ambienceSource.volume = targetVolume;
        }

        ambienceFadeRoutine = null;
    }

    private IEnumerator SetFloorAmbienceDelayedRoutine(int floorNumber, bool escaped, float delaySeconds)
    {
        if (ambienceSource != null)
        {
            ambienceSource.volume = 0f;
        }

        TraceStartup($"Delayed ambience routine entered floor={floorNumber} escaped={escaped} delay={delaySeconds:0.00}");
        if (delaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
        }

        TraceStartup($"Delayed ambience routine firing floor={floorNumber}");
        SetFloorAmbience(floorNumber, escaped);
        delayedAmbienceRoutine = null;
    }

    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = GetSfxSource();
        source.Stop();
        source.clip = clip;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volume * sfxVolume * masterVolume);
        TraceStartup($"PlayClip clip={clip.name} source={source.name} volume={source.volume:0.000} pitch={pitch:0.000}");
        source.Play();
    }

    private void PlayDoorClip(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
        {
            return;
        }

        if (doorSource == null)
        {
            PlayClip(clip, volume, pitch);
            return;
        }

        doorSource.Stop();
        doorSource.clip = clip;
        doorSource.pitch = pitch;
        doorSource.volume = Mathf.Clamp01(volume * sfxVolume * masterVolume);
        TraceStartup($"PlayDoorClip clip={clip.name} volume={doorSource.volume:0.000} pitch={pitch:0.000}");
        doorSource.Play();
    }

    private AudioSource GetSfxSource()
    {
        for (int index = 0; index < sfxSources.Count; index++)
        {
            if (!sfxSources[index].isPlaying)
            {
                return sfxSources[index];
            }
        }

        AudioSource fallback = sfxSources[roundRobinIndex];
        roundRobinIndex = (roundRobinIndex + 1) % sfxSources.Count;
        return fallback;
    }

    private void TraceStartup(string message)
    {
        if (!ShouldTraceStartup())
        {
            return;
        }

        Debug.Log($"[AudioStartupTrace f={Time.frameCount} t={Time.realtimeSinceStartup:0.000}] {message}", this);
    }

    private bool ShouldTraceStartup()
    {
        if (!enableStartupTrace || !Application.isPlaying || Time.realtimeSinceStartup > startupTraceUntilRealtime)
        {
            return false;
        }

        return cachedRuntimeScene.IsValid()
            && RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(cachedRuntimeScene);
    }

    private void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        ambienceVolume = Mathf.Clamp01(ambienceVolume);
        externalMusicVolumeMultiplier = Mathf.Clamp(externalMusicVolumeMultiplier, 1f, 6f);
        walkFootstepBaseVolume = Mathf.Clamp01(walkFootstepBaseVolume);
        sprintFootstepBaseVolume = Mathf.Clamp01(sprintFootstepBaseVolume);
        lowFloorAmbienceMultiplier = Mathf.Clamp01(lowFloorAmbienceMultiplier);
        highFloorAmbienceMultiplier = Mathf.Clamp01(highFloorAmbienceMultiplier);
        escapedAmbienceMultiplier = Mathf.Clamp01(escapedAmbienceMultiplier);

        walkFootstepPitchRange.x = Mathf.Clamp(walkFootstepPitchRange.x, 0.1f, 3f);
        walkFootstepPitchRange.y = Mathf.Clamp(walkFootstepPitchRange.y, 0.1f, 3f);
        sprintFootstepPitchRange.x = Mathf.Clamp(sprintFootstepPitchRange.x, 0.1f, 3f);
        sprintFootstepPitchRange.y = Mathf.Clamp(sprintFootstepPitchRange.y, 0.1f, 3f);
    }

    private void ApplyPersistedRuntimeOptions()
    {
        RLobbyRuntimeOptionsSnapshot snapshot = RLobbyRuntimeOptions.Load(this);
        masterVolume = snapshot.MasterVolume;
        sfxVolume = snapshot.SfxVolume;
        ambienceVolume = PlayerPrefs.HasKey(RLobbyRuntimeOptions.AmbienceVolumeKey)
            ? snapshot.AmbienceVolume
            : Mathf.Min(snapshot.AmbienceVolume, MaxLegacyAmbienceVolume);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        ApplySceneAmbienceForScene(scene, immediate: false);
    }

    private bool ApplySceneAmbienceForCachedScene(bool immediate)
    {
        if (!cachedRuntimeScene.IsValid())
        {
            return false;
        }

        ApplySceneAmbience(cachedRuntimeScene, immediate);
        return true;
    }

    private bool ApplySceneAmbienceForLoadedRuntimeScene(bool immediate)
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);

            if (!IsSceneAmbienceManagedScene(scene))
            {
                continue;
            }

            ApplySceneAmbienceForScene(scene, immediate);
            return true;
        }

        return false;
    }

    private void ApplySceneAmbienceForScene(Scene scene, bool immediate)
    {
        CacheRuntimeScene(scene);
        ApplySceneAmbience(scene, immediate);
    }

    private void CacheRuntimeScene(Scene scene)
    {
        if (scene.IsValid())
        {
            cachedRuntimeScene = scene;
        }
    }

    private static bool IsSceneAmbienceManagedScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return false;
        }

        return MainEscapeSceneIdentityUtility.MatchesCanonicalLobbyScene(scene)
            || MainEscapeSceneIdentityUtility.MatchesCanonicalTutorialScene(scene)
            || MainEscapeSceneIdentityUtility.TryGetCanonicalFloorNumber(scene, out _)
            || RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(scene);
    }

    private void ApplySceneAmbience(Scene scene, bool immediate)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (MainEscapeSceneIdentityUtility.MatchesCanonicalLobbyScene(scene))
        {
            SetLobbyAmbience(immediate);
            return;
        }

        if (MainEscapeSceneIdentityUtility.MatchesCanonicalTutorialScene(scene))
        {
            SetTutorialAmbience();
            return;
        }

        if (TryResolveFloorNumberForAmbience(scene, out int floorNumber))
        {
            SetFloorAmbience(floorNumber, escaped: false);
        }
    }

    private static bool TryResolveFloorNumberForAmbience(Scene scene, out int floorNumber)
    {
        if (Application.isPlaying)
        {
            RRunSessionController sessionController = RRunSessionResolver.ResolveForScene(scene);

            if (sessionController != null)
            {
                return sessionController.TryResolveFloorNumberForScene(scene, out floorNumber);
            }
        }

        if (MainEscapeSceneIdentityUtility.TryGetCanonicalFloorNumber(scene, out floorNumber))
        {
            return true;
        }

        floorNumber = 0;
        return false;
    }

    private void SetLobbyAmbience(bool immediate)
    {
        EnsureRuntimeAudioReady();

        if (ambienceSource == null)
        {
            return;
        }

        AudioClip ambienceClip = ResolveLobbyAmbienceClip();
        float targetVolume = masterVolume * ambienceVolume * 0.9f;

        if (ambienceClip != null)
        {
            targetVolume *= externalMusicVolumeMultiplier;
        }

        TraceStartup($"SetLobbyAmbience targetVolume={targetVolume:0.000}");
        EnsureAmbienceLoopStarted(ambienceClip != null ? ambienceClip : soundBank?.ambienceLoop, startupOffsetSeconds: 0f);
        ambienceSource.pitch = 0.98f;
        StartAmbienceFade(Mathf.Clamp01(targetVolume), immediate ? 0.15f : 0.8f);
    }

    private void SetTutorialAmbience()
    {
        EnsureRuntimeAudioReady();

        if (delayedAmbienceRoutine != null)
        {
            StopCoroutine(delayedAmbienceRoutine);
            delayedAmbienceRoutine = null;
        }

        if (ambienceFadeRoutine != null)
        {
            StopCoroutine(ambienceFadeRoutine);
            ambienceFadeRoutine = null;
        }

        if (ambienceSource == null)
        {
            return;
        }

        TraceStartup("SetTutorialAmbience stop");
        ambienceSource.volume = 0f;
        ambienceSource.Stop();
        ambienceSource.clip = null;
        ambienceSource.pitch = 1f;
    }

    private AudioClip ResolveLobbyAmbienceClip()
    {
        return soundBank?.lobbyMusicLoop;
    }

    private AudioClip ResolveGameplayAmbienceClip()
    {
        return soundBank?.gameplayMusicLoop;
    }

    private sealed class PrototypeSoundBank
    {
        public AudioClip ambienceLoop;
        public AudioClip lobbyMusicLoop;
        public AudioClip gameplayMusicLoop;
        public AudioClip pickupClip;
        public AudioClip batteryReplaceClip;
        public AudioClip deniedClip;
        public AudioClip doorOpenClip;
        public AudioClip doorCloseClip;
        public AudioClip noiseTrapClip;
        public AudioClip bottleShatterClip;
        public AudioClip bottleImpactClip;
        public AudioClip stairBreachClip;
        public AudioClip floorDropClip;
        public AudioClip arrivalChimeClip;
        public AudioClip escapeClip;
        public AudioClip[] walkFootsteps;
        public AudioClip[] sprintFootsteps;
    }

    private static class PrototypeProceduralAudio
    {
        public static PrototypeSoundBank CreateSoundBank(int sampleRate)
        {
            return new PrototypeSoundBank
            {
                ambienceLoop = CreateAmbienceLoop(sampleRate),
                lobbyMusicLoop = LoadMusicClip(LobbyMusicResourcePath),
                gameplayMusicLoop = LoadMusicClip(GameplayMusicResourcePath),
                pickupClip = LoadOptionalClip(PickupSfxResourcePath) ?? CreatePickupClip(sampleRate),
                batteryReplaceClip = LoadOptionalClip(BatteryReplaceSfxResourcePath),
                deniedClip = LoadOptionalClip(DeniedSfxResourcePath) ?? CreateDeniedClip(sampleRate),
                doorOpenClip = ResolveDoorClip(
                    sampleRate,
                    fallbackClipName: "Prototype_DoorOpen_FromSfx",
                    fallbackDurationSeconds: 0.46f,
                    fallbackSearchStartNormalized: 0f,
                    fallbackSearchEndNormalized: 0.52f,
                    fallbackStrideFrames: 512,
                    proceduralFallbackFactory: CreateDoorOpenClip,
                    preferredResourcePaths: new[]
                    {
                        "Audio/Sfx/DoorOpen",
                        "Audio/Sfx/Door_Open",
                        "Audio/Sfx/door_open",
                        DefaultDoorMechanismSfxResourcePath
                    }),
                doorCloseClip = ResolveDoorClip(
                    sampleRate,
                    fallbackClipName: "Prototype_DoorClose_FromSfx",
                    fallbackDurationSeconds: 0.28f,
                    fallbackSearchStartNormalized: 0.28f,
                    fallbackSearchEndNormalized: 1f,
                    fallbackStrideFrames: 512,
                    proceduralFallbackFactory: CreateDoorCloseClip,
                    preferredResourcePaths: new[]
                    {
                        "Audio/Sfx/DoorClose",
                        "Audio/Sfx/Door_Close",
                        "Audio/Sfx/door_close",
                        DefaultDoorMechanismSfxResourcePath
                    }),
                noiseTrapClip = LoadOptionalClip(NoiseTrapSfxResourcePath) ?? CreateNoiseTrapClip(sampleRate),
                bottleShatterClip = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                    LoadOptionalClip(BottleShatterSfxResourcePath),
                    1f,
                    "GlassShatter3_LoudestOneSecond")
                    ?? CreateBottleShatterClip(sampleRate),
                bottleImpactClip = CreateBottleImpactClip(sampleRate),
                stairBreachClip = null,
                floorDropClip = LoadOptionalClip(ElevatorTransitionSfxResourcePath) ?? CreateFloorDropClip(sampleRate),
                arrivalChimeClip = LoadOptionalClip(ElevatorDingSfxResourcePath) ?? CreateElevatorArrivalChimeClip(sampleRate),
                escapeClip = LoadOptionalClip(ElevatorEscapeSfxResourcePath) ?? CreateEscapeClip(sampleRate),
                walkFootsteps = ResolveFootstepSet(sampleRate, sprinting: false),
                sprintFootsteps = ResolveFootstepSet(sampleRate, sprinting: true)
            };
        }

        private static AudioClip LoadMusicClip(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            AudioClip clip = Resources.Load<AudioClip>(resourcePath);

            if (clip == null)
            {
                Debug.LogWarning($"PrototypeAudioManager could not load music resource '{resourcePath}'.");
            }

            return clip;
        }

        private static AudioClip LoadOptionalClip(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            return Resources.Load<AudioClip>(resourcePath);
        }

        private static AudioClip ResolveDoorClip(
            int sampleRate,
            string fallbackClipName,
            float fallbackDurationSeconds,
            float fallbackSearchStartNormalized,
            float fallbackSearchEndNormalized,
            int fallbackStrideFrames,
            System.Func<int, AudioClip> proceduralFallbackFactory,
            string[] preferredResourcePaths)
        {
            for (int index = 0; preferredResourcePaths != null && index < preferredResourcePaths.Length; index++)
            {
                AudioClip sourceClip = LoadOptionalClip(preferredResourcePaths[index]);

                if (sourceClip == null)
                {
                    continue;
                }

                AudioClip excerptClip = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                    sourceClip,
                    fallbackDurationSeconds,
                    fallbackClipName,
                    searchStartNormalized: fallbackSearchStartNormalized,
                    searchEndNormalized: fallbackSearchEndNormalized,
                    strideFrames: fallbackStrideFrames,
                    edgeFadeSeconds: 0.012f);

                if (excerptClip != null)
                {
                    return excerptClip;
                }

                return sourceClip;
            }

            return proceduralFallbackFactory != null
                ? proceduralFallbackFactory(sampleRate)
                : null;
        }

        private static AudioClip[] CreateFootstepSet(int sampleRate, bool sprinting)
        {
            AudioClip[] clips = new AudioClip[4];

            for (int index = 0; index < clips.Length; index++)
            {
                clips[index] = CreateFootstepClip(sampleRate, index + 1, sprinting);
            }

            return clips;
        }

        private static AudioClip[] ResolveFootstepSet(int sampleRate, bool sprinting)
        {
            AudioClip sourceClip = LoadOptionalClip(WalkFootstepSfxResourcePath);
            AudioClip[] walkSourceFootsteps = CreateFootstepSetFromSource(sourceClip);

            if (!sprinting)
            {
                return walkSourceFootsteps.Length > 0
                    ? walkSourceFootsteps
                    : CreateFootstepSet(sampleRate, sprinting: false);
            }

            AudioClip[] sprintSourceFootsteps = CreateSprintFootstepSetFromWalkVariants(walkSourceFootsteps);
            return sprintSourceFootsteps.Length > 0
                ? sprintSourceFootsteps
                : CreateFootstepSet(sampleRate, sprinting: true);
        }

        private static AudioClip[] CreateFootstepSetFromSource(AudioClip sourceClip)
        {
            if (sourceClip == null)
            {
                return System.Array.Empty<AudioClip>();
            }

            AudioClip[] variants =
            {
                CreateFootstepVariantFromSource(sourceClip, "Walk_PlayerStep_A", 0f, 0.25f, durationSeconds: 0.12f, targetPeak: 0.82f),
                CreateFootstepVariantFromSource(sourceClip, "Walk_PlayerStep_B", 0.18f, 0.5f, durationSeconds: 0.12f, targetPeak: 0.82f),
                CreateFootstepVariantFromSource(sourceClip, "Walk_PlayerStep_C", 0.38f, 0.72f, durationSeconds: 0.12f, targetPeak: 0.82f),
                CreateFootstepVariantFromSource(sourceClip, "Walk_PlayerStep_D", 0.58f, 1f, durationSeconds: 0.12f, targetPeak: 0.82f)
            };

            int validCount = 0;

            for (int index = 0; index < variants.Length; index++)
            {
                if (variants[index] != null)
                {
                    validCount++;
                }
            }

            if (validCount == 0)
            {
                return System.Array.Empty<AudioClip>();
            }

            AudioClip[] filtered = new AudioClip[validCount];
            int filteredIndex = 0;

            for (int index = 0; index < variants.Length; index++)
            {
                if (variants[index] == null)
                {
                    continue;
                }

                filtered[filteredIndex++] = variants[index];
            }

            return filtered;
        }

        private static AudioClip[] CreateSprintFootstepSetFromWalkVariants(AudioClip[] walkVariants)
        {
            if (walkVariants == null || walkVariants.Length == 0)
            {
                return System.Array.Empty<AudioClip>();
            }

            AudioClip[] sprintVariants = new AudioClip[walkVariants.Length];

            for (int index = 0; index < walkVariants.Length; index++)
            {
                string clipName = $"Sprint_PlayerStep_{(char)('A' + index)}";
                sprintVariants[index] = CreateModulatedSprintClip(walkVariants[index], clipName, speedMultiplier: 1.24f, targetPeak: 0.96f);
            }

            return sprintVariants;
        }

        private static AudioClip CreateFootstepVariantFromSource(
            AudioClip sourceClip,
            string clipName,
            float searchStartNormalized,
            float searchEndNormalized,
            float durationSeconds,
            float targetPeak)
        {
            if (sourceClip == null)
            {
                return null;
            }

            AudioClip excerpt = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                durationSeconds: durationSeconds,
                clipName: clipName,
                searchStartNormalized: searchStartNormalized,
                searchEndNormalized: searchEndNormalized,
                strideFrames: 128,
                edgeFadeSeconds: 0.005f);

            return CreateAmplifiedClip(excerpt ?? sourceClip, clipName, targetPeak);
        }

        private static AudioClip CreateAmplifiedClip(AudioClip clip, string clipName, float targetPeak)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0)
            {
                return clip;
            }

            float[] samples = new float[clip.samples * clip.channels];

            if (!clip.GetData(samples, 0))
            {
                return clip;
            }

            float peak = 0f;

            for (int index = 0; index < samples.Length; index++)
            {
                float absolute = Mathf.Abs(samples[index]);
                if (absolute > peak)
                {
                    peak = absolute;
                }
            }

            if (peak <= 0.0001f)
            {
                return clip;
            }

            float gain = Mathf.Max(1f, targetPeak / peak);

            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] = Mathf.Clamp(samples[index] * gain, -1f, 1f);
            }

            AudioClip amplifiedClip = AudioClip.Create(
                clipName,
                clip.samples,
                clip.channels,
                clip.frequency,
                false);
            amplifiedClip.SetData(samples, 0);
            return amplifiedClip;
        }

        private static AudioClip CreateModulatedSprintClip(
            AudioClip sourceClip,
            string clipName,
            float speedMultiplier,
            float targetPeak)
        {
            if (sourceClip == null || sourceClip.samples <= 0 || sourceClip.channels <= 0)
            {
                return sourceClip;
            }

            float clampedSpeed = Mathf.Max(1.01f, speedMultiplier);
            int targetFrames = Mathf.Max(1, Mathf.RoundToInt(sourceClip.samples / clampedSpeed));
            float[] sourceSamples = new float[sourceClip.samples * sourceClip.channels];

            if (!sourceClip.GetData(sourceSamples, 0))
            {
                return sourceClip;
            }

            float[] modulatedSamples = new float[targetFrames * sourceClip.channels];

            for (int frame = 0; frame < targetFrames; frame++)
            {
                float sourceFrame = frame * clampedSpeed;
                int baseFrame = Mathf.Clamp(Mathf.FloorToInt(sourceFrame), 0, sourceClip.samples - 1);
                int nextFrame = Mathf.Min(baseFrame + 1, sourceClip.samples - 1);
                float blend = sourceFrame - baseFrame;

                for (int channel = 0; channel < sourceClip.channels; channel++)
                {
                    float current = sourceSamples[(baseFrame * sourceClip.channels) + channel];
                    float next = sourceSamples[(nextFrame * sourceClip.channels) + channel];
                    modulatedSamples[(frame * sourceClip.channels) + channel] = Mathf.Lerp(current, next, blend);
                }
            }

            AudioClip modulatedClip = AudioClip.Create(
                clipName,
                targetFrames,
                sourceClip.channels,
                sourceClip.frequency,
                false);
            modulatedClip.SetData(modulatedSamples, 0);
            return CreateAmplifiedClip(modulatedClip, clipName, targetPeak);
        }

        private static AudioClip CreateAmbienceLoop(int sampleRate)
        {
            float duration = 6f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float lowBed =
                    Mathf.Sin(Mathf.PI * 2f * 31f * time) * 0.075f +
                    Mathf.Sin(Mathf.PI * 2f * 43f * time + 0.7f) * 0.048f +
                    Mathf.Sin(Mathf.PI * 2f * 57f * time + 1.9f) * 0.026f;
                float midDrone =
                    Mathf.Sin(Mathf.PI * 2f * 94f * time + 0.4f) * 0.024f +
                    Mathf.Sin(Mathf.PI * 2f * 141f * time + 1.1f) * 0.016f;
                float air =
                    Mathf.Sin(Mathf.PI * 2f * 222f * time + 0.3f) * 0.009f +
                    Mathf.Sin(Mathf.PI * 2f * 333f * time + 1.4f) * 0.006f;
                float hissGate =
                    0.42f +
                    Mathf.Sin(Mathf.PI * 2f * (1f / 6f) * time + 0.6f) * 0.12f +
                    Mathf.Sin(Mathf.PI * 2f * 0.5f * time + 1.2f) * 0.05f;
                float hiss = HashNoise(index, 63) * 0.011f * hissGate;
                float swell =
                    0.72f +
                    Mathf.Sin(Mathf.PI * 2f * (1f / 3f) * time) * 0.08f +
                    Mathf.Sin(Mathf.PI * 2f * (2f / 3f) * time + 0.7f) * 0.04f;
                samples[index] = ClampSample((lowBed + midDrone + air + hiss) * swell * 0.17f);
            }

            return CreateClip("Prototype_AmbienceLoop", sampleRate, samples);
        }

        private static AudioClip CreateFootstepClip(int sampleRate, int seed, bool sprinting)
        {
            float duration = sprinting ? 0.12f : 0.16f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, sprinting ? 2.4f : 1.9f);
                float heel = Mathf.Sin(Mathf.PI * 2f * (sprinting ? 86f : 62f) * time + seed * 0.3f) * 0.28f;
                float grit = HashNoise(index, seed) * (sprinting ? 0.38f : 0.24f);
                float body = Mathf.Sin(Mathf.PI * 2f * (sprinting ? 138f : 108f) * time + seed * 0.17f) * 0.22f;
                float sample = (heel + grit + body) * envelope;
                samples[index] = ClampSample(sample * 0.7f);
            }

            return CreateClip(sprinting ? $"Prototype_SprintStep_{seed}" : $"Prototype_WalkStep_{seed}", sampleRate, samples);
        }

        private static AudioClip CreatePickupClip(int sampleRate)
        {
            float duration = 0.32f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.8f);
                float lead = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(640f, 1220f, normalizedTime) * time);
                float harmonic = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(960f, 1800f, normalizedTime) * time + 0.2f) * 0.35f;
                float sparkle = Mathf.Sin(Mathf.PI * 2f * 2400f * time) * Mathf.SmoothStep(0f, 0.08f, normalizedTime) * 0.08f;
                samples[index] = ClampSample((lead + harmonic + sparkle) * envelope * 0.36f);
            }

            return CreateClip("Prototype_Pickup", sampleRate, samples);
        }

        private static AudioClip CreateDeniedClip(int sampleRate)
        {
            float duration = 0.2f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 2.2f);
                float frequency = Mathf.Lerp(210f, 132f, normalizedTime);
                float square = Mathf.Sign(Mathf.Sin(Mathf.PI * 2f * frequency * time));
                float undertone = Mathf.Sin(Mathf.PI * 2f * frequency * 0.5f * time) * 0.18f;
                samples[index] = ClampSample((square * 0.18f + undertone) * envelope);
            }

            return CreateClip("Prototype_Denied", sampleRate, samples);
        }

        private static AudioClip CreateDoorOpenClip(int sampleRate)
        {
            float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float onset = Mathf.SmoothStep(0f, 0.08f, normalizedTime);
                float tail = 1f - normalizedTime;
                float woodEnvelope = onset * Mathf.Pow(tail, 0.75f);
                float latchEnvelope = Mathf.Exp(-18f * normalizedTime);

                float latch = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(430f, 250f, normalizedTime) * time + 0.18f) * latchEnvelope * 0.09f;
                float creakBase = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(118f, 76f, normalizedTime) * time + 0.35f);
                float creakHarmonic = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(214f, 132f, normalizedTime) * time + 0.92f) * 0.52f;
                float grain = HashNoise(index, 71) * (0.18f + 0.34f * (1f - normalizedTime));
                float wobble = Mathf.Sin(Mathf.PI * 2f * (3.1f + normalizedTime * 1.4f) * time + 0.6f) * 0.18f;
                float squeal = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(780f, 520f, normalizedTime) * time + 0.14f) * Mathf.Pow(tail, 1.9f) * onset * 0.035f;
                float woodCreak = (creakBase + creakHarmonic + grain + wobble) * woodEnvelope * 0.2f;

                samples[index] = ClampSample((latch + woodCreak + squeal) * 0.92f);
            }

            return CreateClip("Prototype_DoorOpen", sampleRate, samples);
        }

        private static AudioClip CreateDoorCloseClip(int sampleRate)
        {
            float duration = 0.22f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float thud = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(190f, 104f, normalizedTime) * time) * Mathf.Exp(-8.2f * normalizedTime) * 0.24f;
                float click = Mathf.Sign(Mathf.Sin(Mathf.PI * 2f * 620f * time + 0.6f)) * Mathf.Exp(-18f * normalizedTime) * 0.08f;
                float rattle = HashNoise(index, 79) * Mathf.Exp(-11f * normalizedTime) * 0.1f;
                samples[index] = ClampSample((thud + click + rattle) * 0.84f);
            }

            return CreateClip("Prototype_DoorClose", sampleRate, samples);
        }

        private static AudioClip CreateNoiseTrapClip(int sampleRate)
        {
            float duration = 0.26f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float attack = 1f - Mathf.Exp(-48f * normalizedTime);
                float decay = Mathf.Exp(-7.6f * normalizedTime);
                float envelope = attack * decay;
                float shardTick = HashNoise(index * 5, 91) * Mathf.Exp(-20f * normalizedTime) * 0.24f;
                float crystallineBody = HashNoise(index, 97) * Mathf.Exp(-13.5f * normalizedTime) * 0.16f;
                float primaryRing = Mathf.Sin(Mathf.PI * 2f * 1320f * time + 0.21f) * Mathf.Exp(-8.4f * normalizedTime) * 0.12f;
                float secondaryRing = Mathf.Sin(Mathf.PI * 2f * 2080f * time + 0.76f) * Mathf.Exp(-10.2f * normalizedTime) * 0.09f;
                float microRing = Mathf.Sin(Mathf.PI * 2f * 3180f * time + 0.48f) * Mathf.Exp(-12.6f * normalizedTime) * 0.05f;
                float chirp = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(980f, 1660f, normalizedTime) * time + 0.43f) * Mathf.Exp(-11.4f * normalizedTime) * 0.08f;
                float lowTap = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(220f, 128f, normalizedTime) * time + 0.17f) * Mathf.Exp(-9.2f * normalizedTime) * 0.04f;
                samples[index] = ClampSample((shardTick + crystallineBody + primaryRing + secondaryRing + microRing + chirp + lowTap) * envelope * 0.94f);
            }

            return CreateClip("Prototype_NoiseTrap", sampleRate, samples);
        }

        private static AudioClip CreateBottleShatterClip(int sampleRate)
        {
            float duration = 0.42f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.3f);
                float impact = HashNoise(index, 101) * Mathf.Exp(-18f * normalizedTime) * 0.5f;
                float shardSpray = HashNoise(index * 3, 103) * (0.26f + 0.18f * (1f - normalizedTime));
                float ring =
                    Mathf.Sin(Mathf.PI * 2f * 920f * time + 0.13f) * 0.11f +
                    Mathf.Sin(Mathf.PI * 2f * 1430f * time + 0.58f) * 0.08f +
                    Mathf.Sin(Mathf.PI * 2f * 2140f * time + 0.92f) * 0.05f;
                float lowHit = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(180f, 96f, normalizedTime) * time) * Mathf.Exp(-8f * normalizedTime) * 0.18f;
                samples[index] = ClampSample((impact + shardSpray + ring + lowHit) * envelope * 0.92f);
            }

            return CreateClip("Prototype_BottleShatter", sampleRate, samples);
        }

        private static AudioClip CreateBottleImpactClip(int sampleRate)
        {
            float duration = 0.34f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.45f);
                float thud = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(220f, 110f, normalizedTime) * time) * Mathf.Exp(-7.4f * normalizedTime) * 0.24f;
                float crack = HashNoise(index, 111) * Mathf.Exp(-13f * normalizedTime) * 0.34f;
                float sting =
                    Mathf.Sin(Mathf.PI * 2f * 1320f * time + 0.18f) * 0.08f +
                    Mathf.Sin(Mathf.PI * 2f * 1960f * time + 0.72f) * 0.05f;
                samples[index] = ClampSample((thud + crack + sting) * envelope * 0.96f);
            }

            return CreateClip("Prototype_BottleImpact", sampleRate, samples);
        }

        private static AudioClip CreateStairBreachClip(int sampleRate)
        {
            float duration = 0.36f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float impact = Mathf.Exp(-14f * normalizedTime) * HashNoise(index, 17) * 0.55f;
                float ring = Mathf.Sin(Mathf.PI * 2f * 420f * time) * Mathf.Exp(-7f * normalizedTime) * 0.18f;
                float clang = Mathf.Sin(Mathf.PI * 2f * 860f * time + 0.45f) * Mathf.Exp(-9f * normalizedTime) * 0.08f;
                samples[index] = ClampSample((impact + ring + clang) * 0.58f);
            }

            return CreateClip("Prototype_StairBreach", sampleRate, samples);
        }

        private static AudioClip CreateFloorDropClip(int sampleRate)
        {
            float duration = 0.58f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float whoosh = HashNoise(index, 31) * Mathf.Pow(1f - normalizedTime, 1.3f) * 0.24f;
                float dropTone = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(180f, 62f, normalizedTime) * time) * Mathf.Exp(-3.8f * normalizedTime) * 0.22f;
                float rumble = Mathf.Sin(Mathf.PI * 2f * 48f * time) * Mathf.Exp(-2.8f * normalizedTime) * 0.18f;
                samples[index] = ClampSample((whoosh + dropTone + rumble) * 0.6f);
            }

            return CreateClip("Prototype_FloorDrop", sampleRate, samples);
        }

        private static AudioClip CreateElevatorArrivalChimeClip(int sampleRate)
        {
            float duration = 0.64f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float firstTone = ElevatorBellTone(time, 880f, 0f, 0.24f);
                float secondTone = ElevatorBellTone(time, 660f, 0.26f, 0.28f);
                float airyTail = HashNoise(index, 59) * Mathf.Exp(-5.8f * Mathf.Max(0f, time - 0.08f)) * 0.018f;
                samples[index] = ClampSample((firstTone + secondTone + airyTail) * 0.38f);
            }

            return CreateClip("Prototype_ElevatorArrivalChime", sampleRate, samples);
        }

        private static float ElevatorBellTone(float time, float frequency, float startTime, float duration)
        {
            float localTime = time - startTime;

            if (localTime < 0f || localTime > duration)
            {
                return 0f;
            }

            float normalizedTime = Mathf.Clamp01(localTime / duration);
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(localTime / 0.018f));
            float decay = Mathf.Exp(-4.7f * normalizedTime);
            float fundamental = Mathf.Sin(Mathf.PI * 2f * frequency * localTime);
            float harmonic = Mathf.Sin(Mathf.PI * 2f * frequency * 2f * localTime + 0.2f) * 0.16f;
            float subRing = Mathf.Sin(Mathf.PI * 2f * frequency * 0.5f * localTime + 0.5f) * 0.08f;
            return (fundamental + harmonic + subRing) * attack * decay;
        }

        private static AudioClip CreateEscapeClip(int sampleRate)
        {
            float duration = 0.82f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.5f);
                float noteA = Mathf.Sin(Mathf.PI * 2f * 520f * time) * Mathf.SmoothStep(1f, 0.1f, normalizedTime);
                float noteB = Mathf.Sin(Mathf.PI * 2f * 780f * time + 0.2f) * Mathf.SmoothStep(0f, 1f, normalizedTime) * 0.45f;
                float airy = HashNoise(index, 47) * Mathf.Exp(-6f * normalizedTime) * 0.05f;
                samples[index] = ClampSample((noteA + noteB + airy) * envelope * 0.34f);
            }

            return CreateClip("Prototype_Escape", sampleRate, samples);
        }

        private static AudioClip CreateClip(string name, int sampleRate, float[] samples)
        {
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float HashNoise(int index, int seed)
        {
            float value = Mathf.Sin((index + 1) * 12.9898f + seed * 78.233f) * 43758.5453f;
            return Mathf.Repeat(value, 1f) * 2f - 1f;
        }

        private static float ClampSample(float sample)
        {
            return Mathf.Clamp(sample, -1f, 1f);
        }
    }
}
