using UnityEngine;

[DisallowMultipleComponent]
public sealed class VentEnemySpriteAnimationDriver : MonoBehaviour
{
    private const string VentProfileResourcePath = "MainEscape/EnemyArt/VentEnemy_Venter";

    [SerializeField] private CeilingVentEnemyController controller;
    [SerializeField] private CeilingVentEnemyPrefabBindings prefabBindings;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private VentEnemySpriteProfile profile;
    [SerializeField, Min(0f)] private float movementThreshold = 0.02f;
    [SerializeField, Min(0f)] private float emergePresentationDuration = 0.42f;
    [SerializeField] private bool animateWhileIdle = true;
    [SerializeField, Min(1f)] private float idleFramesPerSecond = 6f;

    private Vector3 lastPosition;
    private bool wasEmerged;
    private float emergedAtTime;
    private int lastAnimatedFrame = -1;

    public void ConfigureDependencies(
        CeilingVentEnemyController configuredController,
        CeilingVentEnemyPrefabBindings configuredPrefabBindings,
        SpriteRenderer configuredBodyRenderer,
        VentEnemySpriteProfile configuredProfile)
    {
        controller = configuredController != null ? configuredController : controller;
        prefabBindings = configuredPrefabBindings != null ? configuredPrefabBindings : prefabBindings;
        bodyRenderer = configuredBodyRenderer != null ? configuredBodyRenderer : bodyRenderer;
        profile = configuredProfile != null ? configuredProfile : profile;
    }

    public void ResolveReferences()
    {
        controller ??= GetComponent<CeilingVentEnemyController>();
        prefabBindings ??= GetComponent<CeilingVentEnemyPrefabBindings>();
        bodyRenderer ??= prefabBindings != null ? prefabBindings.BodyRenderer : GetComponentInChildren<SpriteRenderer>(true);
        profile ??= Resources.Load<VentEnemySpriteProfile>(VentProfileResourcePath);
    }

    private void Awake()
    {
        ResolveReferences();
        lastPosition = transform.position;
        wasEmerged = controller != null && controller.IsEmerged;
        if (wasEmerged)
        {
            emergedAtTime = Time.time;
        }
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        TickAnimationFrame();
    }

    public void TickAnimationFrame()
    {
        if (lastAnimatedFrame == Time.frameCount)
        {
            return;
        }

        lastAnimatedFrame = Time.frameCount;
        ResolveReferences();

        if (controller == null || bodyRenderer == null || profile == null)
        {
            return;
        }

        bool isEmerged = controller.IsEmerged;

        if (isEmerged && !wasEmerged)
        {
            emergedAtTime = Time.time;
        }

        wasEmerged = isEmerged;

        if (!isEmerged || !bodyRenderer.gameObject.activeInHierarchy)
        {
            lastPosition = transform.position;
            return;
        }

        float deltaMagnitude = (transform.position - lastPosition).magnitude;
        bool isMoving = deltaMagnitude > movementThreshold;
        EnemySpriteDirection direction = EnemySpriteDirectionUtility.FromFacing(transform.up);
        Sprite[] sequence;
        float framesPerSecond;

        if (controller.IsRecoveringFromAttack)
        {
            sequence = profile.AttackSprites;
            framesPerSecond = profile.AttackFramesPerSecond;
        }
        else if (Time.time - emergedAtTime <= emergePresentationDuration && profile.EmergeSprites.Length > 0)
        {
            sequence = profile.EmergeSprites;
            framesPerSecond = profile.EmergeFramesPerSecond;
        }
        else if (isMoving)
        {
            sequence = profile.GetCrawlSprites(direction);
            framesPerSecond = profile.CrawlFramesPerSecond;
        }
        else if (animateWhileIdle)
        {
            sequence = profile.GetCrawlSprites(direction);
            framesPerSecond = idleFramesPerSecond;
        }
        else
        {
            sequence = null;
            framesPerSecond = profile.CrawlFramesPerSecond;
        }

        Sprite sprite = ResolveAnimatedSprite(sequence, framesPerSecond, profile.SettleSprite);

        if (sprite != null)
        {
            bodyRenderer.sprite = sprite;
        }

        lastPosition = transform.position;
    }

    private static Sprite ResolveAnimatedSprite(Sprite[] sequence, float framesPerSecond, Sprite fallbackSprite)
    {
        if (sequence == null || sequence.Length == 0)
        {
            return fallbackSprite;
        }

        int startIndex = sequence.Length == 1
            ? 0
            : Mathf.FloorToInt(Time.time * Mathf.Max(1f, framesPerSecond)) % sequence.Length;

        for (int offset = 0; offset < sequence.Length; offset++)
        {
            Sprite candidate = sequence[(startIndex + offset) % sequence.Length];

            if (candidate != null)
            {
                return candidate;
            }
        }

        return fallbackSprite;
    }
}
