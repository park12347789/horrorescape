using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySpriteAnimationDriver : MonoBehaviour
{
    private const string SentryProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Sentry";
    private const string NurseProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Nurse";
    private const string StalkerProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Stalker";
    private const string RoamerProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Roamer";

    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private EnemyPrefabBindings prefabBindings;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private GroundEnemySpriteProfile sentryProfile;
    [SerializeField] private GroundEnemySpriteProfile nurseProfile;
    [SerializeField] private GroundEnemySpriteProfile stalkerProfile;
    [SerializeField] private GroundEnemySpriteProfile roamingProfile;
    [SerializeField] private bool autoSelectProfile = true;
    [SerializeField, Min(0f)] private float movementThreshold = 0.02f;

    private GroundEnemySpriteProfile activeProfile;
    private Vector3 lastPosition;
    private bool initialized;
    private bool referencesResolved;

    public bool PreserveSpriteColors => activeProfile != null && activeProfile.PreserveSpriteColors;

    public void ResolveReferences()
    {
        prefabBindings ??= GetComponent<EnemyPrefabBindings>();
        prefabBindings?.AutoAssign();
        stateMachine ??= prefabBindings != null ? prefabBindings.StateMachine : GetComponent<EnemyStateMachine>();
        bodyRenderer = prefabBindings != null && prefabBindings.BodyRenderer != null
            ? prefabBindings.BodyRenderer
            : GetComponentInChildren<SpriteRenderer>(true);
        sentryProfile ??= Resources.Load<GroundEnemySpriteProfile>(SentryProfileResourcePath);
        nurseProfile ??= Resources.Load<GroundEnemySpriteProfile>(NurseProfileResourcePath);
        stalkerProfile ??= Resources.Load<GroundEnemySpriteProfile>(StalkerProfileResourcePath);
        roamingProfile ??= Resources.Load<GroundEnemySpriteProfile>(RoamerProfileResourcePath);
        activeProfile = ResolveProfile();
        referencesResolved = stateMachine != null && bodyRenderer != null && activeProfile != null;
    }

    private void Awake()
    {
        ResolveReferences();
        lastPosition = transform.position;
        initialized = true;
        ApplyDefaultSprite();
    }

    private void OnEnable()
    {
        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (!referencesResolved || stateMachine == null || bodyRenderer == null)
        {
            ResolveReferences();
        }

        if (stateMachine == null || bodyRenderer == null)
        {
            return;
        }

        activeProfile ??= ResolveProfile();

        if (activeProfile == null)
        {
            return;
        }

        EnemySpriteDirection direction = EnemySpriteDirectionUtility.FromFacing(transform.up);
        float deltaMagnitude = initialized ? (transform.position - lastPosition).magnitude : 0f;
        bool isMoving = IsMovingBySpeed(deltaMagnitude, Time.deltaTime, movementThreshold);
        Sprite[] sequence = stateMachine.IsAttackRecovering
            ? activeProfile.GetAttackSprites()
            : isMoving || stateMachine.CurrentState != EnemyState.Idle
                ? activeProfile.GetWalkSprites(direction)
                : activeProfile.GetIdleSpritesOrWalkFallback(direction);
        float framesPerSecond = stateMachine.IsAttackRecovering
            ? activeProfile.AttackFramesPerSecond
            : activeProfile.LoopFramesPerSecond;
        Sprite sprite = EnemySpriteDirectionUtility.ResolveFrame(sequence, framesPerSecond, activeProfile.DefaultSprite);

        if (sprite != null)
        {
            bodyRenderer.sprite = sprite;
        }

        lastPosition = transform.position;
        initialized = true;
    }

    private GroundEnemySpriteProfile ResolveProfile()
    {
        if (!autoSelectProfile)
        {
            return stalkerProfile ?? sentryProfile ?? nurseProfile ?? roamingProfile;
        }

        string enemyName = gameObject.name;

        if (ContainsEnemyTag(enemyName, "Sentry"))
        {
            return sentryProfile ?? roamingProfile ?? stalkerProfile ?? nurseProfile;
        }

        if (ContainsEnemyTag(enemyName, "Nurse"))
        {
            return nurseProfile ?? stalkerProfile ?? sentryProfile ?? roamingProfile;
        }

        if (ContainsEnemyTag(enemyName, "Stalker"))
        {
            return stalkerProfile ?? nurseProfile ?? sentryProfile ?? roamingProfile;
        }

        return roamingProfile ?? sentryProfile ?? stalkerProfile ?? nurseProfile;
    }

    private void ApplyDefaultSprite()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        activeProfile ??= ResolveProfile();

        if (activeProfile?.DefaultSprite != null)
        {
            bodyRenderer.sprite = activeProfile.DefaultSprite;
        }
    }

    private static bool ContainsEnemyTag(string enemyName, string tag)
    {
        return !string.IsNullOrWhiteSpace(enemyName)
            && enemyName.IndexOf(tag, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsMovingBySpeed(float deltaMagnitude, float deltaTime, float speedThreshold)
    {
        if (deltaMagnitude <= 0f)
        {
            return false;
        }

        float movementSpeed = deltaMagnitude / Mathf.Max(0.0001f, deltaTime);
        return movementSpeed > Mathf.Max(0f, speedThreshold);
    }
}

public enum EnemySpriteDirection
{
    Front,
    Back,
    Left,
    Right
}

public static class EnemySpriteDirectionUtility
{
    public static EnemySpriteDirection FromFacing(Vector2 facing)
    {
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
        {
            return facing.x < 0f ? EnemySpriteDirection.Left : EnemySpriteDirection.Right;
        }

        return facing.y >= 0f ? EnemySpriteDirection.Back : EnemySpriteDirection.Front;
    }

    public static Sprite ResolveFrame(Sprite[] sequence, float framesPerSecond, Sprite fallbackSprite)
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
