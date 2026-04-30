using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStunEffect : MonoBehaviour
{
    private static readonly Color[] DotColors =
    {
        new(1f, 0.95f, 0.45f, 1f),
        new(1f, 0.76f, 0.3f, 1f),
        new(0.96f, 0.88f, 0.58f, 1f)
    };

    private static Sprite sharedDotSprite;

    [SerializeField] private Transform anchor;
    [SerializeField, Min(0f)] private float heightOffset = 0.92f;
    [SerializeField] private int sortingOrder = 28;

    private Transform effectRoot;
    private Transform[] dots;

    public void Configure(Transform configuredAnchor, int configuredSortingOrder, float configuredHeightOffset = 0.92f)
    {
        anchor = configuredAnchor != null ? configuredAnchor : transform;
        sortingOrder = configuredSortingOrder;
        heightOffset = Mathf.Max(0f, configuredHeightOffset);
        EnsureBuilt();
        effectRoot.localPosition = new Vector3(0f, heightOffset, 0f);
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        if (visible)
        {
            EnsureBuilt();
        }

        if (effectRoot != null)
        {
            effectRoot.gameObject.SetActive(visible);
        }
    }

    private void Update()
    {
        if (effectRoot == null || !effectRoot.gameObject.activeSelf || dots == null)
        {
            return;
        }

        if (anchor != null && effectRoot.parent != anchor)
        {
            effectRoot.SetParent(anchor, false);
            effectRoot.localPosition = new Vector3(0f, heightOffset, 0f);
        }

        float baseAngle = Time.time * 220f;

        for (int index = 0; index < dots.Length; index++)
        {
            float angle = (baseAngle + (index * 120f)) * Mathf.Deg2Rad;
            float radius = 0.22f + Mathf.Sin(Time.time * 3.1f + index) * 0.03f;
            dots[index].localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * 0.09f,
                0f);
        }
    }

    private void EnsureBuilt()
    {
        anchor ??= transform;

        if (effectRoot == null)
        {
            EnsureSharedDotSprite();

            GameObject rootObject = new("StunEffect");
            rootObject.transform.SetParent(anchor, false);
            rootObject.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            effectRoot = rootObject.transform;

            dots = new Transform[DotColors.Length];

            for (int index = 0; index < dots.Length; index++)
            {
                GameObject dotObject = new($"Dot_{index + 1}");
                dotObject.transform.SetParent(effectRoot, false);
                dotObject.transform.localScale = new Vector3(0.18f, 0.18f, 1f);

                SpriteRenderer renderer = dotObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sharedDotSprite;
                renderer.color = DotColors[index];
                renderer.sortingOrder = sortingOrder + index;
                dots[index] = dotObject.transform;
            }
        }

        if (effectRoot.parent != anchor)
        {
            effectRoot.SetParent(anchor, false);
        }

        effectRoot.localPosition = new Vector3(0f, heightOffset, 0f);
    }

    private static void EnsureSharedDotSprite()
    {
        if (sharedDotSprite != null)
        {
            return;
        }

        Texture2D texture = new(12, 12, TextureFormat.RGBA32, false);

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                Vector2 normalized = new(
                    (x / (texture.width - 1f)) * 2f - 1f,
                    (y / (texture.height - 1f)) * 2f - 1f);
                texture.SetPixel(x, y, normalized.sqrMagnitude <= 0.82f ? Color.white : Color.clear);
            }
        }

        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        sharedDotSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 12f);
    }
}
