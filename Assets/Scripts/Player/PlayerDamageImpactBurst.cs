using System;

using UnityEngine;

public sealed class PlayerDamageImpactBurst : MonoBehaviour
{
    private const float DefaultDuration = 0.24f;
    private static readonly Vector2 DefaultStartScale = new(0.34f, 0.34f);
    private static readonly Vector2 DefaultEndScale = new(1.12f, 1.12f);
    private static Sprite sharedBurstSprite;

    private readonly Sprite[] emptyFrames = Array.Empty<Sprite>();
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Vector2 driftDirection = Vector2.up;
    private Vector3 originLocalPosition;
    private Color burstColor = Color.white;
    private Vector2 startScale = DefaultStartScale;
    private Vector2 endScale = DefaultEndScale;
    private float duration = DefaultDuration;
    private float driftDistance = 0.18f;
    private int sortingOrder = 168;
    private float spawnTime;
    private bool useAuthoredFrames;

    public void Configure(Vector2 direction, Color color)
    {
        Configure(direction, color, emptyFrames, DefaultDuration, DefaultStartScale, DefaultEndScale, 0.18f, 168);
    }

    public void Configure(
        Vector2 direction,
        Color color,
        Sprite[] animationFrames,
        float animationDuration,
        Vector2 animationStartScale,
        Vector2 animationEndScale,
        float animationDriftDistance,
        int animationSortingOrder)
    {
        driftDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        burstColor = color;
        frames = animationFrames ?? emptyFrames;
        useAuthoredFrames = HasAnyFrame(frames);
        duration = Mathf.Max(0.01f, animationDuration);
        startScale = SanitizeScale(animationStartScale, DefaultStartScale);
        endScale = SanitizeScale(animationEndScale, DefaultEndScale);
        driftDistance = Mathf.Max(0f, animationDriftDistance);
        sortingOrder = animationSortingOrder;
        spawnTime = Time.unscaledTime;
        originLocalPosition = transform.localPosition;
        EnsureRenderer();
        ApplyFrame(0f);
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            EnsureRenderer();
        }

        float age = Mathf.Clamp01((Time.unscaledTime - spawnTime) / duration);
        transform.localPosition = originLocalPosition + (Vector3)(driftDirection * Mathf.Lerp(0f, driftDistance, age));
        transform.localScale = Vector3.Lerp(
            new Vector3(startScale.x, startScale.y, 1f),
            new Vector3(endScale.x, endScale.y, 1f),
            age);
        ApplyFrame(age);
        spriteRenderer.color = new Color(burstColor.r, burstColor.g, burstColor.b, Mathf.Lerp(0.95f, 0f, age));

        if (age >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (!useAuthoredFrames)
        {
            EnsureSharedBurstSprite();
            spriteRenderer.sprite = sharedBurstSprite;
        }

        spriteRenderer.color = burstColor;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void ApplyFrame(float normalizedAge)
    {
        if (!useAuthoredFrames || spriteRenderer == null)
        {
            return;
        }

        int frameIndex = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(normalizedAge * frames.Length));

        for (int offset = 0; offset < frames.Length; offset++)
        {
            int resolvedIndex = (frameIndex + offset) % frames.Length;
            Sprite frame = frames[resolvedIndex];

            if (frame != null)
            {
                spriteRenderer.sprite = frame;
                return;
            }
        }

        EnsureSharedBurstSprite();
        spriteRenderer.sprite = sharedBurstSprite;
    }

    private static bool HasAnyFrame(Sprite[] animationFrames)
    {
        if (animationFrames == null)
        {
            return false;
        }

        for (int index = 0; index < animationFrames.Length; index++)
        {
            if (animationFrames[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 SanitizeScale(Vector2 value, Vector2 fallback)
    {
        if (value.x <= 0f || value.y <= 0f)
        {
            return fallback;
        }

        return value;
    }

    private static void EnsureSharedBurstSprite()
    {
        if (sharedBurstSprite != null)
        {
            return;
        }

        Texture2D texture = new(14, 14, TextureFormat.RGBA32, false);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                bool horizontal = Mathf.Abs(y - 6.5f) <= 1f && Mathf.Abs(x - 6.5f) <= 5.5f;
                bool vertical = Mathf.Abs(x - 6.5f) <= 1f && Mathf.Abs(y - 6.5f) <= 5.5f;
                bool diagonalA = Mathf.Abs(x - y) <= 1;
                bool diagonalB = Mathf.Abs((texture.width - 1 - x) - y) <= 1;
                texture.SetPixel(x, y, horizontal || vertical || diagonalA || diagonalB ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        sharedBurstSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 14f);
    }
}
