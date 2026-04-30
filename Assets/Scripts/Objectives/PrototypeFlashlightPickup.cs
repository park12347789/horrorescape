using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PrototypeFlashlightPickup : PrototypeInventoryPickup
{
    private const string FogGlowObjectName = "FlashlightPickupFogGlow";
    private const int FogGlowMinimumSortingOrder = 96;
    private const int FogGlowSortingPadding = 8;
    private const float FogGlowPixelsPerUnit = 100f;
    private static readonly Color FlashlightColor = Color.white;
    private static readonly Color DefaultFogGlowColor = new(0.92f, 0.98f, 1f, 0.4f);
    private static readonly Vector3 MainSceneFlashlightScale = new(0.14f, 0.14f, 1f);
    private static Sprite sharedFogGlowSprite;
    private static Material sharedFogGlowMaterial;

    [SerializeField] private SpriteRenderer fogGlowRenderer;
    [SerializeField] private Color fogGlowColor = new(0.92f, 0.98f, 1f, 0.4f);
    [SerializeField, Min(0f)] private float fogGlowScale = 4.8f;
    [SerializeField] private Vector3 fogGlowLocalOffset = new(0f, 0.04f, 0f);
    [SerializeField, Min(0f)] private float fogGlowPulseSpeed = 1.35f;
    [SerializeField, Range(0f, 0.5f)] private float fogGlowPulseStrength = 0.16f;

    protected override bool UseDiscoveryVisibility => false;
    protected override Vector3 MainSceneScale => MainSceneFlashlightScale;

    protected override void Awake()
    {
        base.Awake();

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureFogGlowRenderer();
        RefreshFogGlowState();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureFogGlowRenderer();
        RefreshFogGlowState();
    }

    protected override void OnDisable()
    {
        if (fogGlowRenderer != null)
        {
            fogGlowRenderer.enabled = false;
        }

        base.OnDisable();
    }

    private void Reset()
    {
        ConfigureAuthored(
            PrototypeItemCatalog.FlashlightItemId,
            PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
            1,
            FlashlightColor);
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (Application.isPlaying)
        {
            return;
        }

        ConfigureAuthored(
            PrototypeItemCatalog.FlashlightItemId,
            PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
            1,
            FlashlightColor);

        fogGlowColor = DefaultFogGlowColor;
    }

    protected override bool TryCollect(PlayerInventory inventory)
    {
        if (inventory == null || inventory.HasItem(PrototypeItemCatalog.FlashlightItemId))
        {
            return false;
        }

        return inventory.AddItem(
            PrototypeItemCatalog.FlashlightItemId,
            PrototypeItemCatalog.GetDisplayName(PrototypeItemCatalog.FlashlightItemId),
            1);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshFogGlowState();
    }

    private void EnsureFogGlowRenderer()
    {
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();

        if (sourceRenderer == null)
        {
            return;
        }

        if (fogGlowRenderer == null)
        {
            Transform glowTransform = transform.Find(FogGlowObjectName);

            if (glowTransform == null)
            {
                GameObject glowObject = new(FogGlowObjectName);
                glowTransform = glowObject.transform;
                glowTransform.SetParent(transform, false);
            }

            fogGlowRenderer = glowTransform.GetComponent<SpriteRenderer>();

            if (fogGlowRenderer == null)
            {
                fogGlowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        fogGlowRenderer.sprite = GetSharedFogGlowSprite();
        fogGlowRenderer.sharedMaterial = GetSharedFogGlowMaterial();
        fogGlowRenderer.maskInteraction = SpriteMaskInteraction.None;
        fogGlowRenderer.drawMode = SpriteDrawMode.Simple;
        fogGlowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        fogGlowRenderer.receiveShadows = false;
        fogGlowRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        fogGlowRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        fogGlowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        fogGlowRenderer.sortingOrder = ResolveFogGlowSortingOrder(sourceRenderer);

        Transform glowRendererTransform = fogGlowRenderer.transform;
        glowRendererTransform.localPosition = fogGlowLocalOffset;
        glowRendererTransform.localRotation = Quaternion.identity;
        glowRendererTransform.localScale = new Vector3(
            Mathf.Max(0.01f, fogGlowScale),
            Mathf.Max(0.01f, fogGlowScale),
            1f);
    }

    private void RefreshFogGlowState()
    {
        EnsureFogGlowRenderer();

        if (fogGlowRenderer == null)
        {
            return;
        }

        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();

        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            fogGlowRenderer.enabled = false;
            return;
        }

        fogGlowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        fogGlowRenderer.sortingOrder = ResolveFogGlowSortingOrder(sourceRenderer);

        float pulse = 1f;

        if (fogGlowPulseSpeed > 0.0001f && fogGlowPulseStrength > 0.0001f)
        {
            pulse += Mathf.Sin(Time.unscaledTime * fogGlowPulseSpeed) * fogGlowPulseStrength;
        }

        Color glowColor = Color.Lerp(fogGlowColor, sourceRenderer.color, 0.2f);
        glowColor.a = Mathf.Clamp01(fogGlowColor.a * Mathf.Max(0.1f, pulse));

        bool shouldShow = enabled
            && gameObject.activeInHierarchy
            && glowColor.a > 0.01f;

        fogGlowRenderer.enabled = shouldShow;

        if (shouldShow)
        {
            fogGlowRenderer.color = glowColor;
        }
    }

    private int ResolveFogGlowSortingOrder(SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null)
        {
            return FogGlowMinimumSortingOrder;
        }

        if (RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene))
        {
            return Mathf.Max(FogGlowMinimumSortingOrder, sourceRenderer.sortingOrder - FogGlowSortingPadding);
        }

        return sourceRenderer.sortingOrder - 1;
    }

    private static Material GetSharedFogGlowMaterial()
    {
        if (sharedFogGlowMaterial != null)
        {
            return sharedFogGlowMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        shader ??= Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        sharedFogGlowMaterial = new Material(shader)
        {
            name = "FlashlightPickupFogGlowMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedFogGlowMaterial;
    }

    private static Sprite GetSharedFogGlowSprite()
    {
        if (sharedFogGlowSprite != null)
        {
            return sharedFogGlowSprite;
        }

        const int textureSize = 128;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "FlashlightPickupFogGlowSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            float v = ((y / (float)(textureSize - 1)) - 0.5f) * 2f;

            for (int x = 0; x < textureSize; x++)
            {
                float u = ((x / (float)(textureSize - 1)) - 0.5f) * 2f;
                float distance = Mathf.Sqrt((u * u) + (v * v));

                if (distance >= 1f)
                {
                    pixels[(y * textureSize) + x] = Color.clear;
                    continue;
                }

                float radial = 1f - distance;
                radial = Mathf.SmoothStep(0f, 1f, radial);
                float core = 1f - Mathf.SmoothStep(0f, 0.38f, distance);
                float alpha = (radial * 0.56f) + (core * 0.36f);
                pixels[(y * textureSize) + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        sharedFogGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            FogGlowPixelsPerUnit);
        sharedFogGlowSprite.name = "FlashlightPickupFogGlowSprite";
        sharedFogGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedFogGlowSprite;
    }
}
