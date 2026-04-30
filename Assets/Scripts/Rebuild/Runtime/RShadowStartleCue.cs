using UnityEngine;

[System.Serializable]
public struct ShadowStartleCueRequest
{
    public Vector3 WorldPosition;
    public Vector3 VisualScale;
    public Vector2 FacingDirection;
    public Vector2 DriftDirection;
    public bool UseWalkAnimation;
    public float MovementDistance;
    public float FadeInDuration;
    public float HoldDuration;
    public float FadeOutDuration;
    public float TargetAlpha;
    public string SortingLayerName;
    public int SortingOrder;
    public AudioClip RevealClip;
    public float RevealClipVolume;
}

[DisallowMultipleComponent]
public sealed class RShadowStartleCue : MonoBehaviour
{
    private const string ShadowProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_ShadowHumanoid";
    private const string VisualRootName = "VisualRoot";
    private const string BodyArtworkName = "BodyArtwork";

    [SerializeField] private EnemyPrefabBindings presentationBindings;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GroundEnemySpriteProfile spriteProfile;

    private Sprite[] activeSequence = System.Array.Empty<Sprite>();
    private float framesPerSecond = 7f;
    private float fadeInDuration;
    private float holdDuration;
    private float fadeOutDuration;
    private float targetAlpha;
    private float movementDistance;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float elapsedTime;
    private bool configured;

    public bool IsPlaying => configured;
    public bool HasRenderablePresentation => configured && bodyRenderer != null && spriteProfile != null;

    public void Configure(MainEscapeShadowStartleMarker marker, WasdPlayerController playerController)
    {
        if (marker == null)
        {
            enabled = false;
            return;
        }

        Vector2 facing = marker.ResolveFacing(playerController != null ? playerController.transform.position : marker.transform.position + marker.transform.up);
        Configure(new ShadowStartleCueRequest
        {
            WorldPosition = marker.transform.position,
            VisualScale = marker.transform.lossyScale,
            FacingDirection = facing,
            DriftDirection = facing,
            UseWalkAnimation = marker.UseWalkAnimation,
            MovementDistance = marker.MovementDistance,
            FadeInDuration = marker.FadeInDuration,
            HoldDuration = marker.HoldDuration,
            FadeOutDuration = marker.FadeOutDuration,
            TargetAlpha = marker.TargetAlpha,
            SortingLayerName = marker.SortingLayerName,
            SortingOrder = marker.SortingOrder,
            RevealClip = marker.RevealClip,
            RevealClipVolume = marker.RevealClipVolume
        });
    }

    public void Configure(ShadowStartleCueRequest request)
    {
        presentationBindings = EnsurePresentationBindings();
        SpriteRenderer resolvedBodyRenderer = ResolveBodyRenderer();

        if (resolvedBodyRenderer == null)
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleCue)} could not configure because the body SpriteRenderer is missing. The cue will be discarded as hidden or misconfigured.",
                this);
            enabled = false;
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(resolvedBodyRenderer);
        spriteProfile ??= Resources.Load<GroundEnemySpriteProfile>(ShadowProfileResourcePath);

        if (spriteProfile == null)
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleCue)} could not load shadow profile at '{ShadowProfileResourcePath}'. The cue may be invisible because no sprite profile is available.",
                this);
        }

        Vector2 facing = request.FacingDirection.sqrMagnitude > 0.0001f ? request.FacingDirection.normalized : Vector2.down;
        EnemySpriteDirection direction = EnemySpriteDirectionUtility.FromFacing(facing);
        activeSequence = ResolveSequence(request.UseWalkAnimation, direction, spriteProfile);
        framesPerSecond = spriteProfile != null ? spriteProfile.LoopFramesPerSecond : 7f;
        fadeInDuration = Mathf.Max(0.01f, request.FadeInDuration);
        holdDuration = Mathf.Max(0.01f, request.HoldDuration);
        fadeOutDuration = Mathf.Max(0.01f, request.FadeOutDuration);
        targetAlpha = Mathf.Clamp(request.TargetAlpha, 0.05f, 1f);
        movementDistance = Mathf.Max(0f, request.MovementDistance);
        startPosition = request.WorldPosition;
        Vector2 driftDirection = request.DriftDirection.sqrMagnitude > 0.0001f
            ? request.DriftDirection.normalized
            : facing;
        endPosition = startPosition + (Vector3)(driftDirection * movementDistance);
        transform.position = startPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (visualRoot != null)
        {
            visualRoot.localScale = request.VisualScale == Vector3.zero ? Vector3.one : request.VisualScale;
        }

        resolvedBodyRenderer.sortingLayerName = string.IsNullOrWhiteSpace(request.SortingLayerName) ? "Default" : request.SortingLayerName;
        resolvedBodyRenderer.sortingOrder = Mathf.Clamp(request.SortingOrder, 8, 128);

        if (resolvedBodyRenderer.sortingOrder <= 90)
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleCue)} configured with sorting order {resolvedBodyRenderer.sortingOrder}, which may be hidden behind the fog overlay.",
                this);
        }

        ApplySprite(0f);
        EnsureAudioSource();
        PlayRevealAudio(request.RevealClip, request.RevealClipVolume);
        elapsedTime = 0f;
        configured = true;
        enabled = true;
    }

    private void Awake()
    {
        presentationBindings ??= GetComponent<EnemyPrefabBindings>();
        audioSource ??= GetComponent<AudioSource>();
        CachePresentationBindings();
    }

    private void Update()
    {
        if (!configured || bodyRenderer == null)
        {
            return;
        }

        elapsedTime += Time.deltaTime;
        float lifetime = fadeInDuration + holdDuration + fadeOutDuration;

        if (elapsedTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float normalizedLifetime = lifetime > 0.001f ? elapsedTime / lifetime : 1f;
        transform.position = Vector3.Lerp(startPosition, endPosition, Mathf.SmoothStep(0f, 1f, normalizedLifetime));
        ApplySprite(elapsedTime);
    }

    private void EnsureAudioSource()
    {
        audioSource ??= GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
        audioSource.reverbZoneMix = 0f;
    }

    private void PlayRevealAudio(AudioClip revealClip, float revealClipVolume)
    {
        if (revealClip == null || audioSource == null)
        {
            return;
        }

        audioSource.clip = revealClip;
        audioSource.volume = Mathf.Clamp01(revealClipVolume);
        audioSource.Play();
    }

    private void ApplySprite(float animationTime)
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.sprite = ResolveFrameAtTime(
            activeSequence,
            framesPerSecond,
            animationTime,
            spriteProfile != null ? spriteProfile.DefaultSprite : null);
        bodyRenderer.color = new Color(0f, 0f, 0f, EvaluateAlpha(animationTime));
    }

    private float EvaluateAlpha(float animationTime)
    {
        if (animationTime <= fadeInDuration)
        {
            return targetAlpha * Mathf.Clamp01(animationTime / Mathf.Max(0.01f, fadeInDuration));
        }

        float fadeOutStartTime = fadeInDuration + holdDuration;

        if (animationTime <= fadeOutStartTime)
        {
            return targetAlpha;
        }

        float fadeOutElapsed = animationTime - fadeOutStartTime;
        float fadeOutNormalized = 1f - Mathf.Clamp01(fadeOutElapsed / Mathf.Max(0.01f, fadeOutDuration));
        return targetAlpha * fadeOutNormalized;
    }

    private static Sprite[] ResolveSequence(
        bool useWalkAnimation,
        EnemySpriteDirection direction,
        GroundEnemySpriteProfile profile)
    {
        if (profile == null)
        {
            return System.Array.Empty<Sprite>();
        }

        if (useWalkAnimation)
        {
            Sprite[] walk = profile.GetWalkSprites(direction);

            if (walk != null && walk.Length > 0)
            {
                return walk;
            }
        }

        Sprite[] idle = profile.GetIdleSprites(direction);
        return idle ?? System.Array.Empty<Sprite>();
    }

    private static Sprite ResolveFrameAtTime(Sprite[] sequence, float framesPerSecond, float animationTime, Sprite fallbackSprite)
    {
        if (sequence == null || sequence.Length == 0)
        {
            return fallbackSprite;
        }

        if (sequence.Length == 1)
        {
            return sequence[0];
        }

        int index = Mathf.FloorToInt(animationTime * Mathf.Max(1f, framesPerSecond)) % sequence.Length;
        return sequence[index];
    }

    private EnemyPrefabBindings EnsurePresentationBindings()
    {
        presentationBindings ??= GetComponent<EnemyPrefabBindings>();

        if (presentationBindings != null)
        {
            CachePresentationBindings();
            return presentationBindings;
        }

        Transform visualRoot = transform.Find(VisualRootName);

        if (visualRoot == null)
        {
            GameObject visualRootObject = new(VisualRootName);
            visualRoot = visualRootObject.transform;
            visualRoot.SetParent(transform, false);
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        Transform bodyArtwork = visualRoot.Find(BodyArtworkName);

        if (bodyArtwork == null)
        {
            GameObject bodyArtworkObject = new(BodyArtworkName);
            bodyArtwork = bodyArtworkObject.transform;
            bodyArtwork.SetParent(visualRoot, false);
            bodyArtwork.localPosition = Vector3.zero;
            bodyArtwork.localRotation = Quaternion.identity;
            bodyArtwork.localScale = Vector3.one;
        }

        SpriteRenderer bodyRenderer = bodyArtwork.GetComponent<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            bodyRenderer = bodyArtwork.gameObject.AddComponent<SpriteRenderer>();
        }

        presentationBindings = gameObject.AddComponent<EnemyPrefabBindings>();
        CachePresentationBindings();
        return presentationBindings;
    }

    private SpriteRenderer ResolveBodyRenderer()
    {
        if (bodyRenderer != null)
        {
            return bodyRenderer;
        }

        CachePresentationBindings();
        return bodyRenderer;
    }

    private void CachePresentationBindings()
    {
        presentationBindings ??= GetComponent<EnemyPrefabBindings>();

        if (presentationBindings == null)
        {
            return;
        }

        presentationBindings.AutoAssign();
        visualRoot = presentationBindings.VisualRoot;
        bodyRenderer = presentationBindings.BodyRenderer;
    }
}
