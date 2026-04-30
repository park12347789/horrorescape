using UnityEngine;

[DisallowMultipleComponent]
public sealed class VentEnemyAudioDriver : MonoBehaviour
{
    private const int SampleRate = 22050;
    private const string VentMovementResourcePath = "Audio/Sfx/MetalStairsFootSteps_sagamusix_CC0";
    private const string VentEmergeResourcePath = "Audio/Sfx/mechanical1_BMacZero_CC0";
    private static AudioClip sharedVentMovementSourceClip;
    private static AudioClip sharedVentEmergeSourceClip;
    private static AudioClip[] sharedCrawlLoopVariants;
    private static AudioClip sharedCrawlPulseClip;
    private static AudioClip sharedNodeStepClip;
    private static AudioClip sharedEmergeClip;

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField, Min(0.5f)] private float minAudibleDistance = 1.4f;
    [SerializeField, Min(1f)] private float maxAudibleDistance = 15f;
    [SerializeField, Range(0f, 1f)] private float crawlPulseVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float nodeStepVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float transitionVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float followGlobalSfxMix = 1f;
    [SerializeField, Min(0.05f)] private float crawlLoopHoldDuration = 0.4f;
    [SerializeField, Min(0.25f)] private float crawlLoopVariantRefreshInterval = 0.9f;

    private AudioSource crawlSource;
    private AudioSource accentSource;
    private AudioClip crawlLoopClip;
    private AudioClip[] crawlLoopVariants;
    private AudioClip crawlPulseClip;
    private AudioClip nodeStepClip;
    private AudioClip emergeClip;
    private AudioClip retreatClip;
    private AudioClip ventMovementSourceClip;
    private AudioClip ventEmergeSourceClip;
    private bool crawlLoopActive;
    private float crawlLoopPitch = 1f;
    private int currentCrawlLoopVariantIndex = -1;
    private float crawlLoopAudibleUntil;
    private float nextCrawlLoopVariantRefreshTime;

    public void Initialize(WasdPlayerController player)
    {
        playerController = player;
        EnsureAudio();
    }

    public void SetCrawlLoopActive(bool active)
    {
        EnsureAudio();

        if (!active)
        {
            crawlLoopActive = false;
            StopCrawlLoop();
            return;
        }

        NotifyCrawlMovement();
    }

    public void OnCrawlPulse()
    {
        NotifyCrawlMovement(forceVariantRefresh: true);
        PlayClip(accentSource, crawlPulseClip, crawlPulseVolume * 0.42f, Random.Range(0.96f, 1.03f));
    }

    public void OnVentNodeReached()
    {
        NotifyCrawlMovement(forceVariantRefresh: true);
        PlayClip(accentSource, nodeStepClip, nodeStepVolume, Random.Range(0.94f, 1.01f));
    }

    public void OnEmerge()
    {
        SetCrawlLoopActive(false);
        PlayClip(accentSource, emergeClip, transitionVolume, Random.Range(0.95f, 1f));
    }

    public void OnRetreat()
    {
        SetCrawlLoopActive(false);
        PlayClip(accentSource, emergeClip, transitionVolume * 0.92f, Random.Range(0.93f, 0.99f));
    }

    public void NotifyCrawlMovement(bool forceVariantRefresh = false)
    {
        EnsureAudio();

        if (crawlLoopVariants == null || crawlLoopVariants.Length == 0)
        {
            return;
        }

        crawlLoopActive = true;
        crawlLoopAudibleUntil = Time.time + crawlLoopHoldDuration;

        if (forceVariantRefresh || Time.time >= nextCrawlLoopVariantRefreshTime || !crawlSource.isPlaying)
        {
            RefreshCrawlLoopVariant(forceVariantRefresh);
        }

        EnsureCrawlLoopPlaying();
    }

    private void Awake()
    {
        EnsureAudio();
    }

    private void OnDisable()
    {
        SetCrawlLoopActive(false);
    }

    private void Update()
    {
        UpdateCrawlLoopMix();
    }

    private void EnsureAudio()
    {
        crawlSource ??= CreateSource("VentCrawlAudio");
        accentSource ??= CreateSource("VentAccentAudio");
        ventMovementSourceClip ??= ResolveSharedVentMovementSourceClip();
        ventEmergeSourceClip ??= ResolveSharedVentEmergeSourceClip();
        crawlLoopVariants ??= ResolveSharedCrawlLoopVariants(ventMovementSourceClip);
        crawlPulseClip ??= ResolveSharedCrawlPulseClip(ventMovementSourceClip);
        crawlLoopClip ??= ResolveInitialCrawlLoopClip() ?? crawlPulseClip;
        nodeStepClip ??= ResolveSharedNodeStepClip(ventMovementSourceClip);
        emergeClip ??= ResolveSharedEmergeClip(ventEmergeSourceClip);
        retreatClip = emergeClip;
    }

    private AudioSource CreateSource(string objectName)
    {
        GameObject sourceObject = new(objectName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.reverbZoneMix = 0.65f;
        return source;
    }

    private void EnsureCrawlLoopPlaying()
    {
        if (!crawlLoopActive || crawlSource == null || crawlLoopClip == null)
        {
            return;
        }

        if (crawlSource.clip != crawlLoopClip)
        {
            crawlSource.Stop();
            crawlSource.clip = crawlLoopClip;
            crawlSource.loop = true;
            crawlSource.time = 0f;
        }

        crawlSource.pitch = crawlLoopPitch;

        if (!crawlSource.isPlaying)
        {
            crawlSource.Play();
        }
    }

    private void StopCrawlLoop()
    {
        if (crawlSource == null)
        {
            return;
        }

        crawlSource.loop = false;
        crawlSource.Stop();
        crawlSource.clip = null;
        crawlSource.volume = 0f;
        crawlLoopAudibleUntil = 0f;
    }

    private void UpdateCrawlLoopMix()
    {
        if (crawlLoopActive && Time.time > crawlLoopAudibleUntil)
        {
            crawlLoopActive = false;
        }

        if (!crawlLoopActive || crawlSource == null || crawlLoopClip == null)
        {
            StopCrawlLoop();
            return;
        }

        EnsureCrawlLoopPlaying();

        float attenuation = CalculateAttenuation();
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);
        crawlSource.volume = Mathf.Clamp01(crawlPulseVolume * attenuation * sfxMixScalar);
    }

    private void PlayClip(AudioSource source, AudioClip clip, float volume, float pitch)
    {
        if (source == null || clip == null)
        {
            return;
        }

        float attenuation = CalculateAttenuation();
        float sfxMixScalar = Mathf.Lerp(1f, PrototypeAudioManager.GetRuntimeSfxMixScalar(), followGlobalSfxMix);

        if (attenuation <= 0.01f || sfxMixScalar <= 0.001f)
        {
            return;
        }

        source.pitch = pitch;
        source.PlayOneShot(clip, Mathf.Clamp01(volume * attenuation * sfxMixScalar));
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
        return Mathf.Pow(softened, 1.18f);
    }

    private void OnValidate()
    {
        minAudibleDistance = Mathf.Max(0.5f, minAudibleDistance);
        maxAudibleDistance = Mathf.Max(minAudibleDistance, maxAudibleDistance);
        crawlPulseVolume = Mathf.Clamp01(crawlPulseVolume);
        nodeStepVolume = Mathf.Clamp01(nodeStepVolume);
        transitionVolume = Mathf.Clamp01(transitionVolume);
        followGlobalSfxMix = Mathf.Clamp01(followGlobalSfxMix);
    }

    private void RefreshCrawlLoopVariant(bool forceChange)
    {
        AudioClip nextClip = SelectNextCrawlLoopClip(forceChange);

        if (nextClip == null)
        {
            return;
        }

        bool clipChanged = crawlLoopClip != nextClip;
        crawlLoopClip = nextClip;
        crawlLoopPitch = Random.Range(0.97f, 1.03f);
        nextCrawlLoopVariantRefreshTime = Time.time + crawlLoopVariantRefreshInterval;

        if (clipChanged && crawlSource != null && crawlSource.isPlaying)
        {
            crawlSource.Stop();
            crawlSource.clip = null;
        }
    }

    private AudioClip SelectNextCrawlLoopClip(bool forceChange)
    {
        if (crawlLoopVariants == null || crawlLoopVariants.Length == 0)
        {
            return crawlLoopClip;
        }

        if (crawlLoopVariants.Length == 1)
        {
            currentCrawlLoopVariantIndex = 0;
            return crawlLoopVariants[0];
        }

        int nextIndex = currentCrawlLoopVariantIndex;

        if (forceChange || nextIndex < 0)
        {
            do
            {
                nextIndex = Random.Range(0, crawlLoopVariants.Length);
            }
            while (nextIndex == currentCrawlLoopVariantIndex);
        }

        currentCrawlLoopVariantIndex = Mathf.Clamp(nextIndex, 0, crawlLoopVariants.Length - 1);
        return crawlLoopVariants[currentCrawlLoopVariantIndex];
    }

    private AudioClip ResolveInitialCrawlLoopClip()
    {
        if (crawlLoopVariants == null || crawlLoopVariants.Length == 0)
        {
            return null;
        }

        currentCrawlLoopVariantIndex = 0;
        return crawlLoopVariants[0];
    }

    private static AudioClip[] CreateCrawlLoopVariants(AudioClip sourceClip)
    {
        AudioClip[] variants =
        {
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.88f,
                "Vent_CrawlLoop_MetalStairs_A",
                searchStartNormalized: 0f,
                searchEndNormalized: 0.35f,
                strideFrames: 1024,
                edgeFadeSeconds: 0.012f),
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.92f,
                "Vent_CrawlLoop_MetalStairs_B",
                searchStartNormalized: 0.22f,
                searchEndNormalized: 0.62f,
                strideFrames: 1024,
                edgeFadeSeconds: 0.012f),
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.96f,
                "Vent_CrawlLoop_MetalStairs_C",
                searchStartNormalized: 0.48f,
                searchEndNormalized: 0.95f,
                strideFrames: 1024,
                edgeFadeSeconds: 0.012f)
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

    private static AudioClip ResolveSharedVentMovementSourceClip()
    {
        sharedVentMovementSourceClip ??= LoadOptionalClip(VentMovementResourcePath);
        return sharedVentMovementSourceClip;
    }

    private static AudioClip ResolveSharedVentEmergeSourceClip()
    {
        sharedVentEmergeSourceClip ??= LoadOptionalClip(VentEmergeResourcePath);
        return sharedVentEmergeSourceClip;
    }

    private static AudioClip[] ResolveSharedCrawlLoopVariants(AudioClip sourceClip)
    {
        if (sharedCrawlLoopVariants != null)
        {
            return sharedCrawlLoopVariants;
        }

        sharedCrawlLoopVariants = CreateCrawlLoopVariants(sourceClip);
        return sharedCrawlLoopVariants;
    }

    private static AudioClip ResolveSharedCrawlPulseClip(AudioClip sourceClip)
    {
        if (sharedCrawlPulseClip != null)
        {
            return sharedCrawlPulseClip;
        }

        sharedCrawlPulseClip = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.24f,
                "Vent_CrawlPulse_MetalStairs",
                searchStartNormalized: 0f,
                searchEndNormalized: 0.45f,
                strideFrames: 2048)
            ?? VentEnemySoundFactory.CreateCrawlPulseClip(SampleRate);
        return sharedCrawlPulseClip;
    }

    private static AudioClip ResolveSharedNodeStepClip(AudioClip sourceClip)
    {
        if (sharedNodeStepClip != null)
        {
            return sharedNodeStepClip;
        }

        sharedNodeStepClip = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.34f,
                "Vent_NodeStep_MetalStairs",
                searchStartNormalized: 0.35f,
                searchEndNormalized: 1f,
                strideFrames: 2048)
            ?? VentEnemySoundFactory.CreateNodeStepClip(SampleRate);
        return sharedNodeStepClip;
    }

    private static AudioClip ResolveSharedEmergeClip(AudioClip sourceClip)
    {
        if (sharedEmergeClip != null)
        {
            return sharedEmergeClip;
        }

        sharedEmergeClip = AudioClipExcerptUtility.CreateLoudestExcerptClip(
                sourceClip,
                0.52f,
                "Vent_Emerge_Mechanical",
                searchStartNormalized: 0f,
                searchEndNormalized: 1f,
                strideFrames: 512)
            ?? VentEnemySoundFactory.CreateEmergeClip(SampleRate);
        return sharedEmergeClip;
    }

    private static AudioClip LoadOptionalClip(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            return null;
        }

        return Resources.Load<AudioClip>(resourcePath);
    }

    private static class VentEnemySoundFactory
    {
        private static AudioClip sharedFallbackCrawlPulseClip;
        private static AudioClip sharedFallbackNodeStepClip;
        private static AudioClip sharedFallbackEmergeClip;
        private static AudioClip sharedFallbackRetreatClip;

        public static AudioClip CreateCrawlPulseClip(int sampleRate)
        {
            if (sharedFallbackCrawlPulseClip != null)
            {
                return sharedFallbackCrawlPulseClip;
            }

            float duration = 0.24f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime / 0.08f));
                float release = Mathf.Pow(1f - normalizedTime, 1.45f);
                float envelope = attack * release;
                float tick = Mathf.Sin(Mathf.PI * 2f * 780f * time) * 0.12f;
                float scrape = Mathf.Sin(Mathf.PI * 2f * 1180f * time + 0.4f) * 0.08f;
                float hollowResonance = CreateResonance(time, normalizedTime, 186f, 372f, 0.38f) * 0.34f;
                float pipeBuzz = CreateResonance(time, normalizedTime, 268f, 536f, 1.2f) * 0.16f;
                float grit = HashNoise(index, 11) * 0.1f;
                samples[index] = ClampSample((tick + scrape + hollowResonance + pipeBuzz + grit) * envelope * 0.78f);
            }

            ApplyEchoTail(samples, sampleRate, 0.028f, 0.52f, 4);
            sharedFallbackCrawlPulseClip = CreateClip("Vent_CrawlPulse", sampleRate, samples);
            return sharedFallbackCrawlPulseClip;
        }

        public static AudioClip CreateNodeStepClip(int sampleRate)
        {
            if (sharedFallbackNodeStepClip != null)
            {
                return sharedFallbackNodeStepClip;
            }

            float duration = 0.34f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.2f);
                float knock = Mathf.Sin(Mathf.PI * 2f * 390f * time) * Mathf.Exp(-10f * normalizedTime) * 0.24f;
                float ring = CreateResonance(time, normalizedTime, 612f, 1210f, 0.2f) * 0.28f;
                float undertone = CreateResonance(time, normalizedTime, 208f, 418f, 0.65f) * 0.18f;
                float metalBuzz = HashNoise(index, 23) * 0.1f;
                samples[index] = ClampSample((knock + ring + undertone + metalBuzz) * envelope * 0.76f);
            }

            ApplyEchoTail(samples, sampleRate, 0.036f, 0.56f, 4);
            sharedFallbackNodeStepClip = CreateClip("Vent_NodeStep", sampleRate, samples);
            return sharedFallbackNodeStepClip;
        }

        public static AudioClip CreateEmergeClip(int sampleRate)
        {
            if (sharedFallbackEmergeClip != null)
            {
                return sharedFallbackEmergeClip;
            }

            float duration = 0.52f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float impact = HashNoise(index, 37) * Mathf.Exp(-14f * normalizedTime) * 0.34f;
                float scrapeBurst = HashNoise(index * 2, 41) * Mathf.Exp(-9f * normalizedTime) * 0.16f;
                float clang = CreateResonance(time, normalizedTime, 428f, 856f, 0.18f) * 0.3f;
                float overtone = CreateResonance(time, normalizedTime, 982f, 1480f, 0.35f) * 0.14f;
                float hollowWhoosh = Mathf.Sin(Mathf.PI * 2f * 92f * time + 0.4f) * Mathf.Exp(-3.4f * normalizedTime) * 0.18f;
                samples[index] = ClampSample((impact + scrapeBurst + clang + overtone + hollowWhoosh) * 0.78f);
            }

            ApplyEchoTail(samples, sampleRate, 0.044f, 0.6f, 5);
            sharedFallbackEmergeClip = CreateClip("Vent_Emerge", sampleRate, samples);
            return sharedFallbackEmergeClip;
        }

        public static AudioClip CreateRetreatClip(int sampleRate)
        {
            if (sharedFallbackRetreatClip != null)
            {
                return sharedFallbackRetreatClip;
            }

            float duration = 0.46f;
            int sampleCount = Mathf.CeilToInt(duration * sampleRate);
            float[] samples = new float[sampleCount];

            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)sampleRate;
                float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
                float envelope = Mathf.Pow(1f - normalizedTime, 1.12f);
                float rattle = HashNoise(index, 51) * envelope * 0.16f;
                float plate = CreateResonance(time, normalizedTime, 504f, 1010f, 0.22f) * 0.24f;
                float undertone = CreateResonance(time, normalizedTime, 178f, 356f, 0.3f) * 0.18f;
                float hiss = HashNoise(index * 3, 57) * Mathf.Exp(-6f * normalizedTime) * 0.08f;
                samples[index] = ClampSample((rattle + plate + undertone + hiss) * 0.74f);
            }

            ApplyEchoTail(samples, sampleRate, 0.041f, 0.58f, 5);
            sharedFallbackRetreatClip = CreateClip("Vent_Retreat", sampleRate, samples);
            return sharedFallbackRetreatClip;
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

        private static float CreateResonance(float time, float normalizedTime, float fundamentalHz, float overtoneHz, float phase)
        {
            float decay = Mathf.Exp(-4.8f * normalizedTime);
            float primary = Mathf.Sin(Mathf.PI * 2f * fundamentalHz * time + phase) * 0.72f;
            float overtone = Mathf.Sin(Mathf.PI * 2f * overtoneHz * time + phase * 1.7f) * 0.28f;
            return (primary + overtone) * decay;
        }

        private static void ApplyEchoTail(float[] samples, int sampleRate, float delaySeconds, float decay, int tapCount)
        {
            if (samples == null || samples.Length == 0 || tapCount <= 0)
            {
                return;
            }

            int delaySamples = Mathf.Max(1, Mathf.RoundToInt(delaySeconds * sampleRate));
            float[] dry = (float[])samples.Clone();

            for (int index = 0; index < samples.Length; index++)
            {
                float mixed = dry[index];

                for (int tap = 1; tap <= tapCount; tap++)
                {
                    int echoIndex = index - (delaySamples * tap);

                    if (echoIndex < 0)
                    {
                        break;
                    }

                    mixed += dry[echoIndex] * Mathf.Pow(decay, tap);
                }

                samples[index] = ClampSample(mixed);
            }
        }

        private static float ClampSample(float sample)
        {
            return Mathf.Clamp(sample, -1f, 1f);
        }
    }
}
