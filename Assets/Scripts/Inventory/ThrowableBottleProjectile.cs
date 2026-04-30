using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ThrowableBottleProjectile : MonoBehaviour
{
    private const int EnemyProbeResultCapacity = 32;
    private static Sprite sharedBottleSprite;

    [SerializeField, Min(0f)] private float throwSpeed = 11.5f;
    [SerializeField, Min(0f)] private float maxTravelDistance = 6.4f;
    [SerializeField, Min(0f)] private float shatterNoiseRadius = 6.8f;
    [SerializeField, Min(0f)] private float stunDurationMin = 2f;
    [SerializeField, Min(0f)] private float stunDurationMax = 2f;
    [SerializeField, Min(0.05f)] private float enemyProbeRadius = 0.32f;
    [SerializeField, Min(0.05f)] private float enemyProbeStepDistance = 0.18f;
    [SerializeField, Min(0f)] private float stunTargetForgivenessRadius = 0.12f;
    [SerializeField] private NoiseSystem noiseSystem;

    private Rigidbody2D body;
    private CircleCollider2D circleCollider;
    private SpriteRenderer spriteRenderer;
    private Collider2D ignoredCollider;
    private Transform ownerTransform;
    private Rigidbody2D ownerBody;
    private Vector2 startPosition;
    private Vector2 lastProbePosition;
    private int ownerInstanceId;
    private bool shattered;
    private INoiseEventBus noiseEventBus;
    private readonly Collider2D[] enemyProbeResults = new Collider2D[EnemyProbeResultCapacity];

    public static ThrowableBottleProjectile Spawn(
        Vector2 position,
        Vector2 direction,
        Collider2D ownerCollider,
        float configuredThrowSpeed,
        float configuredMaxTravelDistance,
        float configuredNoiseRadius,
        float configuredStunDurationMin,
        float configuredStunDurationMax,
        int configuredOwnerInstanceId,
        INoiseEventBus configuredNoiseEventBus = null)
    {
        GameObject projectileObject = new("ThrownGlassBottle");
        projectileObject.transform.position = position;
        ThrowableBottleProjectile projectile = projectileObject.AddComponent<ThrowableBottleProjectile>();
        projectile.Configure(
            direction,
            ownerCollider,
            configuredThrowSpeed,
            configuredMaxTravelDistance,
            configuredNoiseRadius,
            configuredStunDurationMin,
            configuredStunDurationMax,
            configuredOwnerInstanceId,
            configuredNoiseEventBus);
        return projectile;
    }

    private void Awake()
    {
        EnsureComponents();
    }

    public void Configure(
        Vector2 direction,
        Collider2D ownerCollider,
        float configuredThrowSpeed,
        float configuredMaxTravelDistance,
        float configuredNoiseRadius,
        float configuredStunDurationMin,
        float configuredStunDurationMax,
        int configuredOwnerInstanceId,
        INoiseEventBus configuredNoiseEventBus = null)
    {
        EnsureComponents();

        noiseEventBus = configuredNoiseEventBus;
        ignoredCollider = ownerCollider;
        ownerTransform = ownerCollider != null ? ownerCollider.transform : null;
        ownerBody = ownerCollider != null ? ownerCollider.attachedRigidbody : null;
        ownerInstanceId = configuredOwnerInstanceId;
        throwSpeed = Mathf.Max(0f, configuredThrowSpeed);
        maxTravelDistance = Mathf.Max(0.1f, configuredMaxTravelDistance);
        shatterNoiseRadius = Mathf.Max(0.1f, configuredNoiseRadius);
        stunDurationMin = Mathf.Max(0f, configuredStunDurationMin);
        stunDurationMax = Mathf.Max(stunDurationMin, configuredStunDurationMax);
        startPosition = transform.position;
        lastProbePosition = startPosition;

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.up;

        transform.up = normalizedDirection;
        body.linearVelocity = normalizedDirection * throwSpeed;
        body.angularVelocity = -320f;

        if (ignoredCollider != null)
        {
            Physics2D.IgnoreCollision(circleCollider, ignoredCollider, true);
        }
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    private void Update()
    {
        if (shattered)
        {
            return;
        }

        ProbeEnemyAlongTravel();

        if (shattered)
        {
            return;
        }

        if (Vector2.Distance(startPosition, transform.position) >= maxTravelDistance)
        {
            Shatter(transform.position, false);
        }

        lastProbePosition = transform.position;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (shattered || collision.collider == null || ShouldIgnore(collision.collider))
        {
            return;
        }

        bool hitEnemy = TryHitEnemy(collision.collider, out bool stunnedEnemy) && stunnedEnemy;
        Vector2 impactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)transform.position;
        Shatter(impactPoint, hitEnemy);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (shattered || other == null || ShouldIgnore(other))
        {
            return;
        }

        if (!TryHitEnemy(other, out bool hitEnemy))
        {
            return;
        }

        Vector2 impactPoint = other.ClosestPoint(transform.position);
        Shatter(impactPoint, hitEnemy);
    }

    private bool TryHitEnemy(Collider2D other, out bool stunnedEnemy)
    {
        stunnedEnemy = false;
        float duration = Random.Range(stunDurationMin, stunDurationMax);

        EnemyStateMachine groundEnemy = other.GetComponentInParent<EnemyStateMachine>();

        if (groundEnemy != null)
        {
            stunnedEnemy = groundEnemy.TryApplyStun(duration);
            return true;
        }

        CeilingVentEnemyController ventEnemy = other.GetComponentInParent<CeilingVentEnemyController>();

        if (ventEnemy != null)
        {
            stunnedEnemy = ventEnemy.TryApplyStun(duration);
            return true;
        }

        return false;
    }

    private bool ShouldIgnore(Collider2D other)
    {
        if (other == null)
        {
            return true;
        }

        if (ignoredCollider != null && other == ignoredCollider)
        {
            return true;
        }

        if (ownerBody != null && other.attachedRigidbody != null && other.attachedRigidbody == ownerBody)
        {
            return true;
        }

        return ownerTransform != null
            && (other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform));
    }

    private void ProbeEnemyAlongTravel()
    {
        Vector2 currentPosition = transform.position;
        float segmentDistance = Vector2.Distance(lastProbePosition, currentPosition);

        if (TryHitRegisteredStunTarget(lastProbePosition, currentPosition, out Vector2 registryImpactPoint, out bool registryHitEnemy))
        {
            Shatter(registryImpactPoint, registryHitEnemy);
            return;
        }

        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(segmentDistance / Mathf.Max(0.05f, enemyProbeStepDistance)));

        for (int index = 0; index <= sampleCount; index++)
        {
            float t = sampleCount <= 0 ? 1f : index / (float)sampleCount;
            Vector2 samplePoint = Vector2.Lerp(lastProbePosition, currentPosition, t);

            if (TryFindEnemyHit(samplePoint, out Vector2 impactPoint, out bool hitEnemy))
            {
                Shatter(impactPoint, hitEnemy);
                return;
            }
        }
    }

    private bool TryFindEnemyHit(Vector2 samplePoint, out Vector2 impactPoint, out bool hitEnemy)
    {
        impactPoint = samplePoint;
        hitEnemy = false;
        int hitCount = Physics2D.OverlapCircle(
            samplePoint,
            enemyProbeRadius,
            ContactFilter2D.noFilter,
            enemyProbeResults);

        for (int index = 0; index < hitCount; index++)
        {
            Collider2D overlap = enemyProbeResults[index];

            if (overlap == null || ShouldIgnore(overlap))
            {
                continue;
            }

            if (!TryHitEnemy(overlap, out bool stunnedEnemy))
            {
                continue;
            }

            impactPoint = overlap.ClosestPoint(samplePoint);
            hitEnemy = stunnedEnemy;
            return true;
        }

        return false;
    }

    private bool TryHitRegisteredStunTarget(Vector2 segmentStart, Vector2 segmentEnd, out Vector2 impactPoint, out bool hitEnemy)
    {
        impactPoint = segmentEnd;
        hitEnemy = false;
        var targets = ThrowableStunTargetRegistry.Targets;

        for (int index = 0; index < targets.Count; index++)
        {
            IThrowableStunTarget target = targets[index];
            Object targetObject = target as Object;

            if (target == null || targetObject == null || !target.CanBeStunnedByThrowable)
            {
                continue;
            }

            if (ownerTransform != null
                && targetObject is Component component
                && (component.transform == ownerTransform || component.transform.IsChildOf(ownerTransform)))
            {
                continue;
            }

            Vector2 aimPoint = target.ThrowableStunAimPoint;
            float allowedDistance = Mathf.Max(0.01f, target.ThrowableStunHitRadius + enemyProbeRadius + stunTargetForgivenessRadius);
            float distanceToPath = DistanceToSegment(aimPoint, segmentStart, segmentEnd, out Vector2 closestPointOnPath);

            if (distanceToPath > allowedDistance)
            {
                continue;
            }

            float duration = Random.Range(stunDurationMin, stunDurationMax);
            hitEnemy = target.TryApplyThrowableStun(duration);
            impactPoint = closestPointOnPath;
            return true;
        }

        return false;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd, out Vector2 closestPoint)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.0001f)
        {
            closestPoint = segmentStart;
            return Vector2.Distance(point, segmentStart);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, segment) / lengthSquared);
        closestPoint = segmentStart + (segment * t);
        return Vector2.Distance(point, closestPoint);
    }

    private void Shatter(Vector2 position, bool hitEnemy)
    {
        if (shattered)
        {
            return;
        }

        shattered = true;
        body.linearVelocity = Vector2.zero;
        ResolveNoiseEventBus()?.TryEmitNoise(position, shatterNoiseRadius, NoiseSourceType.Collision, ownerInstanceId);
        PrototypeAudioManager.TryPlayBottleShatter(hitEnemy);

        GameObject burstObject = new("BottleShatterBurst");
        burstObject.transform.position = position;
        BottleShatterBurst burst = burstObject.AddComponent<BottleShatterBurst>();
        burst.Configure(hitEnemy ? new Color(1f, 0.9f, 0.42f, 1f) : new Color(0.62f, 0.92f, 1f, 1f));

        Destroy(gameObject);
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private void EnsureComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (circleCollider == null)
        {
            circleCollider = GetComponent<CircleCollider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        EnsureSharedBottleSprite();
        spriteRenderer.sprite = sharedBottleSprite;
        spriteRenderer.color = new Color(0.67f, 0.9f, 1f, 1f);
        spriteRenderer.sortingOrder = 145;

        circleCollider.radius = 0.14f;
        circleCollider.offset = Vector2.zero;
        circleCollider.isTrigger = false;

        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.freezeRotation = false;
    }

    private static void EnsureSharedBottleSprite()
    {
        if (sharedBottleSprite != null)
        {
            return;
        }

        Texture2D texture = new(10, 18, TextureFormat.RGBA32, false);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                texture.SetPixel(x, y, Color.clear);
            }
        }

        for (int y = 2; y <= 12; y++)
        {
            for (int x = 2; x <= 7; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        for (int y = 13; y <= 15; y++)
        {
            texture.SetPixel(3, y, Color.white);
            texture.SetPixel(4, y, Color.white);
            texture.SetPixel(5, y, Color.white);
            texture.SetPixel(6, y, Color.white);
        }

        texture.SetPixel(4, 16, Color.white);
        texture.SetPixel(5, 16, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        sharedBottleSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 14f);
    }

    private sealed class BottleShatterBurst : MonoBehaviour
    {
        private static Sprite sharedBurstSprite;

        private SpriteRenderer spriteRenderer;
        private float spawnTime;
        private Color burstColor = Color.white;

        public void Configure(Color color)
        {
            burstColor = color;
            spawnTime = Time.time;
            EnsureRenderer();
        }

        private void Update()
        {
            if (spriteRenderer == null)
            {
                EnsureRenderer();
            }

            float age = Mathf.Clamp01((Time.time - spawnTime) / 0.22f);
            transform.localScale = Vector3.Lerp(new Vector3(0.2f, 0.2f, 1f), new Vector3(0.85f, 0.85f, 1f), age);
            spriteRenderer.color = new Color(burstColor.r, burstColor.g, burstColor.b, Mathf.Lerp(0.9f, 0f, age));

            if (age >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            EnsureSharedBurstSprite();
            spriteRenderer.sprite = sharedBurstSprite;
            spriteRenderer.color = burstColor;
            spriteRenderer.sortingOrder = 146;
        }

        private static void EnsureSharedBurstSprite()
        {
            if (sharedBurstSprite != null)
            {
                return;
            }

            Texture2D texture = new(12, 12, TextureFormat.RGBA32, false);

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    bool diagonal = Mathf.Abs(x - y) <= 1 || Mathf.Abs((texture.width - 1 - x) - y) <= 1;
                    bool plus = Mathf.Abs(x - 5.5f) <= 0.8f || Mathf.Abs(y - 5.5f) <= 0.8f;
                    texture.SetPixel(x, y, diagonal || plus ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sharedBurstSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 12f);
        }
    }
}
