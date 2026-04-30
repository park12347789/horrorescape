using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(EnemyStateMachine))]
public sealed class PrototypeEnemyAudioDriver : MonoBehaviour
{
    private const string CommonEnemyFootstepSfxResourcePath = "Audio/Sfx/FootstepsBootsTileMono_NoxSound_CC0";

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField, Min(0.5f)] private float minAudibleDistance = 1.0f;
    [SerializeField, Min(1f)] private float maxAudibleDistance = 13.2f;
    [SerializeField, Range(0f, 1f)] private float nearLoopVolume = 0.1f;
    [SerializeField, Range(0f, 1f)] private float nearStepVolume = 0.2f;
    [SerializeField, Range(0f, 1f)] private float investigateAccentVolume = 0.12f;
    [SerializeField, Range(0f, 1f)] private float chaseAccentVolume = 0.28f;
    [SerializeField, Min(0.05f)] private float stateAccentCooldown = 0.28f;
    [SerializeField, Range(0f, 1f)] private float followGlobalSfxMix = 1f;
    [SerializeField, Min(0.02f)] private float movementThreshold = 0.025f;

    private EnemyStateMachine stateMachine;
    private AudioSource loopSource;
    private AudioSource stepSource;
    private AudioSource accentSource;
    private AudioClip loopClip;
    private AudioClip[] stepClips;
    private AudioClip investigateAccentClip;
    private AudioClip chaseAccentClip;
    private Vector3 lastPosition;
    private float nextStepTime;
    private float nextAccentAllowedTime;
    private float currentLoopVolume;
    private EnemyState lastState = EnemyState.Idle;
    private const float LoopStartThreshold = 0.0035f;
    private const float LoopStopThreshold = 0.0015f;
    private static AudioClip[] sharedCommonStepClips;

    public void Initialize(WasdPlayerController player)
    {
        playerController = player;
        stateMachine ??= GetComponent<EnemyStateMachine>();
        lastPosition = transform.position;
        nextStepTime = Time.time + 0.25f;
        lastState = stateMachine != null ? stateMachine.CurrentState : EnemyState.Idle;
        stepClips = null;
        EnsureAudio();
    }

    private void Awake()
    {
        stateMachine = GetComponent<EnemyStateMachine>();
        lastPosition = transform.position;
        lastState = stateMachine != null ? stateMachine.CurrentState : EnemyState.Idle;
        EnsureAudio();
    }

    private void Update()
    {
        if (stateMachine == null || playerController == null)
        {
            currentLoopVolume = 0f;
            StopLoopIfSilent();
            lastPosition = transform.position;
            return;
        }

        float speed = Vector2.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        bool isMoving = speed > movementThreshold;
        Vector2 toPlayer = (Vector2)transform.position - (Vector2)playerController.transform.position;
        float attenuation = CalculateAttenuation(toPlayer.sqrMagnitude);
        EnemyState state = stateMachine.CurrentState;
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);
        float movementOrAlertFactor = isMoving ? 1f : GetIdleLoopMultiplier(state);
        float targetLoopVolume = nearLoopVolume * attenuation * sfxMixScalar * GetLoopStateMultiplier(state) * movementOrAlertFactor;

        currentLoopVolume = Mathf.Lerp(currentLoopVolume, targetLoopVolume, 1f - Mathf.Exp(-7f * Time.deltaTime));
        loopSource.pitch = GetLoopPitch(state, isMoving);
        UpdateLoopPlayback(targetLoopVolume);

        if (isMoving && attenuation > 0.012f && Time.time >= nextStepTime)
        {
            PlayStep(state, attenuation, sfxMixScalar);
            nextStepTime = Time.time + GetStepInterval(state);
        }

        TryPlayStateAccent(state, attenuation, sfxMixScalar);
        lastState = state;
        lastPosition = transform.position;
    }

    private void PlayStep(EnemyState state, float attenuation, float sfxMixScalar)
    {
        if (stepSource == null || stepClips == null || stepClips.Length == 0)
        {
            return;
        }

        AudioClip clip = stepClips[Random.Range(0, stepClips.Length)];
        if (clip == null)
        {
            stepClips = ResolveStepSet();
            if (stepClips == null || stepClips.Length == 0)
            {
                return;
            }

            clip = stepClips[Random.Range(0, stepClips.Length)];
            if (clip == null)
            {
                return;
            }
        }

        stepSource.pitch = Random.Range(0.95f, 1.03f) + GetStepPitchOffset(state);
        stepSource.volume = nearStepVolume * attenuation * sfxMixScalar * GetStepVolumeMultiplier(state);
        stepSource.PlayOneShot(clip);
    }

    private void TryPlayStateAccent(EnemyState state, float attenuation, float sfxMixScalar)
    {
        if (accentSource == null || state == lastState || Time.time < nextAccentAllowedTime || attenuation <= 0.01f)
        {
            return;
        }

        float volume = 0f;
        AudioClip clip = null;
        float pitchMin = 0.98f;
        float pitchMax = 1.02f;

        switch (state)
        {
            case EnemyState.Investigate:
                volume = investigateAccentVolume;
                clip = investigateAccentClip;
                pitchMin = 0.97f;
                pitchMax = 1.03f;
                break;
            case EnemyState.Chase:
                volume = chaseAccentVolume;
                clip = chaseAccentClip;
                pitchMin = 0.95f;
                pitchMax = 1.02f;
                break;
        }

        if (clip == null || volume <= 0f)
        {
            return;
        }

        accentSource.pitch = Random.Range(pitchMin, pitchMax);
        accentSource.PlayOneShot(clip, Mathf.Clamp01(volume * attenuation * sfxMixScalar));
        nextAccentAllowedTime = Time.time + stateAccentCooldown;
    }

    private float CalculateAttenuation(float sqrDistance)
    {
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
        return Mathf.Pow(softened, 1.35f);
    }

    private static float GetLoopStateMultiplier(EnemyState state)
    {
        return state switch
        {
            EnemyState.Investigate => 1.08f,
            EnemyState.Chase => 1.46f,
            EnemyState.Search => 1.12f,
            _ => 0.86f
        };
    }

    private static float GetLoopPitch(EnemyState state, bool isMoving)
    {
        float basePitch = state switch
        {
            EnemyState.Investigate => 0.94f,
            EnemyState.Chase => 1.08f,
            EnemyState.Search => 0.98f,
            _ => 0.9f
        };

        return isMoving ? basePitch : basePitch - 0.05f;
    }

    private static float GetIdleLoopMultiplier(EnemyState state)
    {
        return state switch
        {
            EnemyState.Investigate => 0.16f,
            EnemyState.Chase => 0.28f,
            EnemyState.Search => 0.13f,
            _ => 0f
        };
    }

    private static float GetStepInterval(EnemyState state)
    {
        return state switch
        {
            EnemyState.Investigate => 0.33f,
            EnemyState.Chase => 0.22f,
            EnemyState.Search => 0.38f,
            _ => 0.52f
        };
    }

    private static float GetStepVolumeMultiplier(EnemyState state)
    {
        return state switch
        {
            EnemyState.Investigate => 0.96f,
            EnemyState.Chase => 1.34f,
            EnemyState.Search => 1.04f,
            _ => 0.9f
        };
    }

    private static float GetStepPitchOffset(EnemyState state)
    {
        return state switch
        {
            EnemyState.Investigate => 0.01f,
            EnemyState.Chase => 0.06f,
            EnemyState.Search => 0.02f,
            _ => -0.02f
        };
    }

    private void EnsureAudio()
    {
        if (loopSource == null)
        {
            loopSource = GetComponent<AudioSource>();
            loopSource.playOnAwake = false;
            loopSource.loop = true;
            loopSource.spatialBlend = 0f;
            loopSource.dopplerLevel = 0f;
            loopSource.reverbZoneMix = 0f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
        }

        if (stepSource == null)
        {
            GameObject stepObject = new("EnemyStepSource");
            stepObject.transform.SetParent(transform, false);
            stepSource = stepObject.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.loop = false;
            stepSource.spatialBlend = 0f;
            stepSource.dopplerLevel = 0f;
            stepSource.reverbZoneMix = 0f;
            stepSource.rolloffMode = AudioRolloffMode.Linear;
        }

        if (accentSource == null)
        {
            GameObject accentObject = new("EnemyAccentSource");
            accentObject.transform.SetParent(transform, false);
            accentSource = accentObject.AddComponent<AudioSource>();
            accentSource.playOnAwake = false;
            accentSource.loop = false;
            accentSource.spatialBlend = 0f;
            accentSource.dopplerLevel = 0f;
            accentSource.reverbZoneMix = 0f;
            accentSource.rolloffMode = AudioRolloffMode.Linear;
        }

        if (loopClip == null)
        {
            loopClip = PrototypeEnemySoundFactory.CreateLoopClip();
            loopSource.clip = loopClip;
            loopSource.volume = 0f;
        }

        if (stepClips == null || stepClips.Length == 0)
        {
            stepClips = ResolveStepSet();
        }

        investigateAccentClip ??= PrototypeEnemySoundFactory.CreateInvestigateAccentClip();
        chaseAccentClip ??= PrototypeEnemySoundFactory.CreateChaseAccentClip();
    }

    private void UpdateLoopPlayback(float targetLoopVolume)
    {
        if (loopSource == null || loopClip == null)
        {
            return;
        }

        if (!loopSource.isPlaying)
        {
            if (targetLoopVolume <= LoopStartThreshold)
            {
                loopSource.volume = 0f;
                return;
            }

            loopSource.time = Random.Range(0f, Mathf.Max(0.05f, loopClip.length - 0.05f));
            loopSource.volume = 0f;
            loopSource.Play();
        }

        loopSource.volume = currentLoopVolume;
        StopLoopIfSilent();
    }

    private void StopLoopIfSilent()
    {
        if (loopSource == null || !loopSource.isPlaying)
        {
            return;
        }

        if (currentLoopVolume > LoopStopThreshold)
        {
            return;
        }

        loopSource.Stop();
        loopSource.volume = 0f;
    }

    private void OnValidate()
    {
        minAudibleDistance = Mathf.Max(0.5f, minAudibleDistance);
        maxAudibleDistance = Mathf.Max(minAudibleDistance, maxAudibleDistance);
        nearLoopVolume = Mathf.Clamp01(nearLoopVolume);
        nearStepVolume = Mathf.Clamp01(nearStepVolume);
        investigateAccentVolume = Mathf.Clamp01(investigateAccentVolume);
        chaseAccentVolume = Mathf.Clamp01(chaseAccentVolume);
        stateAccentCooldown = Mathf.Max(0.05f, stateAccentCooldown);
        followGlobalSfxMix = Mathf.Clamp01(followGlobalSfxMix);
        movementThreshold = Mathf.Max(0.02f, movementThreshold);
    }

    private AudioClip[] ResolveStepSet()
    {
        AudioClip sourceClip = Resources.Load<AudioClip>(CommonEnemyFootstepSfxResourcePath);
        AudioClip[] commonSteps = CreateStepSetFromSource(sourceClip);
        return commonSteps.Length > 0
            ? commonSteps
            : PrototypeEnemySoundFactory.CreateStepSet();
    }

    private static AudioClip[] CreateStepSetFromSource(AudioClip sourceClip)
    {
        if (sourceClip == null)
        {
            return System.Array.Empty<AudioClip>();
        }

        if (sharedCommonStepClips != null && sharedCommonStepClips.Length > 0)
        {
            return sharedCommonStepClips;
        }

        AudioClip[] variants =
        {
            CreateStepVariantFromSource(sourceClip, "GroundEnemy_Step_A", 0f, 0.22f),
            CreateStepVariantFromSource(sourceClip, "GroundEnemy_Step_B", 0.16f, 0.42f),
            CreateStepVariantFromSource(sourceClip, "GroundEnemy_Step_C", 0.36f, 0.68f),
            CreateStepVariantFromSource(sourceClip, "GroundEnemy_Step_D", 0.58f, 1f)
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

        sharedCommonStepClips = filtered;
        return filtered;
    }

    private static AudioClip CreateStepVariantFromSource(
        AudioClip sourceClip,
        string clipName,
        float searchStartNormalized,
        float searchEndNormalized)
    {
        if (sourceClip == null)
        {
            return null;
        }

        AudioClip excerpt = AudioClipExcerptUtility.CreateLoudestExcerptClip(
            sourceClip,
            durationSeconds: 0.11f,
            clipName: clipName,
            searchStartNormalized: searchStartNormalized,
            searchEndNormalized: searchEndNormalized,
            strideFrames: 96,
            edgeFadeSeconds: 0.004f);

        return excerpt ?? sourceClip;
    }

    private static class PrototypeEnemySoundFactory
    {
        private const int SampleRate = 22050;
        private static AudioClip sharedLoopClip;
        private static AudioClip[] sharedStepClips;
        private static AudioClip sharedInvestigateAccentClip;
        private static AudioClip sharedChaseAccentClip;

        public static AudioClip CreateLoopClip()
        {
            if (sharedLoopClip != null)
            {
                return sharedLoopClip;
            }

            float duration = 1.6f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float lowHum =
                    Mathf.Sin(Mathf.PI * 2f * 64f * time) * 0.18f +
                    Mathf.Sin(Mathf.PI * 2f * 82f * time + 0.5f) * 0.12f;
                float hiss =
                    Mathf.Sin(Mathf.PI * 2f * 210f * time + 1.1f) * 0.04f +
                    HashNoise(index, 5) * 0.016f;
                float sway = 0.76f + Mathf.Sin(Mathf.PI * 2f * 0.75f * time) * 0.12f;
                samples[index] = Mathf.Clamp((lowHum + hiss) * sway * 0.44f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Prototype_EnemyLoop", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            sharedLoopClip = clip;
            return sharedLoopClip;
        }

        public static AudioClip[] CreateStepSet()
        {
            if (sharedStepClips != null && sharedStepClips.Length > 0)
            {
                return sharedStepClips;
            }

            AudioClip[] clips = new AudioClip[4];

            for (int index = 0; index < clips.Length; index++)
            {
                clips[index] = CreateStepClip(index + 1);
            }

            sharedStepClips = clips;
            return sharedStepClips;
        }

        public static AudioClip CreateInvestigateAccentClip()
        {
            if (sharedInvestigateAccentClip != null)
            {
                return sharedInvestigateAccentClip;
            }

            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float normalized = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalized, 1.8f);
                float tone = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(340f, 280f, normalized) * time) * 0.24f;
                float texture = HashNoise(index, 91) * 0.08f;
                samples[index] = Mathf.Clamp((tone + texture) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Prototype_EnemyInvestigateAccent", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            sharedInvestigateAccentClip = clip;
            return sharedInvestigateAccentClip;
        }

        public static AudioClip CreateChaseAccentClip()
        {
            if (sharedChaseAccentClip != null)
            {
                return sharedChaseAccentClip;
            }

            const float duration = 0.24f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float normalized = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalized, 1.45f);
                float low = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(210f, 160f, normalized) * time + 0.2f) * 0.28f;
                float high = Mathf.Sin(Mathf.PI * 2f * Mathf.Lerp(520f, 410f, normalized) * time + 0.5f) * 0.12f;
                float grit = HashNoise(index, 127) * 0.1f;
                samples[index] = Mathf.Clamp((low + high + grit) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Prototype_EnemyChaseAccent", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            sharedChaseAccentClip = clip;
            return sharedChaseAccentClip;
        }

        private static AudioClip CreateStepClip(int seed)
        {
            float duration = 0.16f;
            int sampleCount = Mathf.CeilToInt(duration * SampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 2.1f);
                float thump = Mathf.Sin(Mathf.PI * 2f * 74f * time + seed * 0.2f) * 0.22f;
                float scrape = HashNoise(index, 13 + seed) * 0.28f;
                float click = Mathf.Sin(Mathf.PI * 2f * 360f * time + seed * 0.37f) * 0.06f;
                samples[index] = Mathf.Clamp((thump + scrape + click) * envelope * 0.55f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create($"Prototype_EnemyStep_{seed}", samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float HashNoise(int index, int seed)
        {
            float value = Mathf.Sin((index + 1) * 12.9898f + seed * 78.233f) * 43758.5453f;
            return Mathf.Repeat(value, 1f) * 2f - 1f;
        }
    }
}
