using System.Collections;

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class RTutorialBottleTarget : MonoBehaviour, IThrowableStunTarget
{
    private const int SortingOrder = 145;
    private static Sprite sharedTargetSprite;

    [SerializeField] private Transform aimAnchor;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField, Min(0.05f)] private float hitRadius = 0.78f;
    [SerializeField] private Color readyColor = new(0.78f, 0.22f, 0.2f, 1f);
    [SerializeField] private Color stunnedColor = new(0.48f, 0.72f, 1f, 1f);

    private Coroutine stunRoutine;
    private bool stunned;

    public bool CanBeStunnedByThrowable => isActiveAndEnabled;
    public Vector3 ThrowableStunAimPoint => aimAnchor != null ? aimAnchor.position : transform.position;
    public float ThrowableStunHitRadius => hitRadius;

    public bool TryApplyThrowableStun(float duration)
    {
        stunned = true;
        ApplyVisualState();

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
        }

        stunRoutine = StartCoroutine(ClearStunAfterDelay(Mathf.Max(0.35f, duration)));
        return true;
    }

    private void Reset()
    {
        CacheReferences();
        ApplyVisualState();
    }

    private void Awake()
    {
        CacheReferences();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        ThrowableStunTargetRegistry.Register(this);
    }

    private void OnDisable()
    {
        ThrowableStunTargetRegistry.Unregister(this);
    }

    private void OnValidate()
    {
        hitRadius = Mathf.Max(0.05f, hitRadius);
        CacheReferences();
        ApplyVisualState();
    }

    private IEnumerator ClearStunAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        stunned = false;
        stunRoutine = null;
        ApplyVisualState();
    }

    private void CacheReferences()
    {
        aimAnchor ??= transform;
        bodyRenderer ??= ResolveBodyRenderer();

        if (bodyRenderer != null && bodyRenderer.sprite == null)
        {
            bodyRenderer.sprite = GetTargetSprite();
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.sortingOrder = SortingOrder;
        }

        EnsureCollider();
    }

    private SpriteRenderer ResolveBodyRenderer()
    {
        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();

        if (rootRenderer != null && rootRenderer.sprite != null)
        {
            return rootRenderer;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null && renderer.sprite != null)
            {
                if (rootRenderer != null && rootRenderer != renderer && rootRenderer.sprite == null)
                {
                    rootRenderer.enabled = false;
                }

                return renderer;
            }
        }

        return rootRenderer;
    }

    private void ApplyVisualState()
    {
        if (bodyRenderer == null)
        {
            return;
        }

        bodyRenderer.color = stunned ? stunnedColor : readyColor;
    }

    private void EnsureCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = hitRadius;
    }

    private static Sprite GetTargetSprite()
    {
        if (sharedTargetSprite != null)
        {
            return sharedTargetSprite;
        }

        Texture2D texture = new(16, 16, TextureFormat.RGBA32, false)
        {
            name = "RTutorialBottleTargetSprite",
            hideFlags = HideFlags.HideAndDontSave
        };
        Color clear = new(1f, 1f, 1f, 0f);
        Color[] pixels = new Color[16 * 16];

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 7.5f;
                float dy = y - 7.5f;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                pixels[(y * 16) + x] = distance <= 7.3f ? Color.white : clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        sharedTargetSprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
        sharedTargetSprite.name = "RTutorialBottleTargetSprite";
        sharedTargetSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedTargetSprite;
    }
}
