using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MainEscapePropBlockerAuthoring))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class MainEscapeSolidBlockerAuthoring : MonoBehaviour
{
    private const int PreviewSortingOrder = 2001;
    private const float MinimumColliderAxis = 0.05f;
    private const string GeneratedVisualRootName = "__RuntimeSolidBlockerVisual";
    private static Sprite generatedSprite;

    private enum RuntimeVisualStyle
    {
        None = 0,
        DividerScreen = 1,
    }

    [SerializeField] private Vector2Int footprint = Vector2Int.one;
    [SerializeField] private Color visualColor = new(1f, 0.46f, 0.22f, 0.78f);

    [Header("Blocking Shape")]
    [SerializeField] private Vector2 colliderLocalSize = Vector2.one;
    [SerializeField] private Vector2 colliderLocalOffset = Vector2.zero;

    [Header("Runtime Visual")]
    [SerializeField] private RuntimeVisualStyle runtimeVisualStyle = RuntimeVisualStyle.None;
    [SerializeField, Min(0)] private int runtimeVisualSortingOrder = 12;
    [SerializeField] private Color runtimeFrameColor = new(0.42f, 0.24f, 0.2f, 1f);
    [SerializeField] private Color runtimePanelColor = new(0.72f, 0.48f, 0.42f, 0.98f);
    [SerializeField] private Color runtimeHighlightColor = new(0.84f, 0.66f, 0.58f, 0.94f);
    [SerializeField, Range(0.35f, 0.98f)] private float runtimeMajorAxisFill = 0.9f;
    [SerializeField, Range(0.12f, 0.95f)] private float runtimeMinorAxisFill = 0.52f;
    [SerializeField, Range(0.04f, 0.35f)] private float runtimeCapThickness = 0.12f;
    [SerializeField, Range(0.04f, 0.28f)] private float runtimeBandThickness = 0.08f;

    public Vector2Int Footprint => footprint;

    public void Configure(Vector2Int configuredFootprint, Color configuredVisualColor)
    {
        footprint = new Vector2Int(
            Mathf.Max(1, configuredFootprint.x),
            Mathf.Max(1, configuredFootprint.y));
        visualColor = configuredVisualColor;
        transform.localScale = new Vector3(footprint.x, footprint.y, ResolveDepthScale());
        ApplyAuthoringState(allowHierarchyMutation: true);
    }

    private void Reset()
    {
        SyncFootprintFromPlacedScale();
        ApplyAuthoringState(allowHierarchyMutation: true);
    }

    private void OnEnable()
    {
        SyncFootprintFromPlacedScale();
        ApplyAuthoringState(allowHierarchyMutation: true);
    }

    private void OnValidate()
    {
        SyncFootprintFromPlacedScale();
        colliderLocalSize = new Vector2(
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.x),
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.y));
        runtimeMajorAxisFill = Mathf.Clamp(runtimeMajorAxisFill, 0.35f, 0.98f);
        runtimeMinorAxisFill = Mathf.Clamp(runtimeMinorAxisFill, 0.12f, 0.95f);
        runtimeCapThickness = Mathf.Clamp(runtimeCapThickness, 0.04f, 0.35f);
        runtimeBandThickness = Mathf.Clamp(runtimeBandThickness, 0.04f, 0.28f);
        ApplyAuthoringState(allowHierarchyMutation: false);
#if UNITY_EDITOR
        QueueEditorRefresh();
#endif
    }

    private void ApplyAuthoringState(bool allowHierarchyMutation)
    {
        MainEscapePropBlockerAuthoring propBlocker = GetComponent<MainEscapePropBlockerAuthoring>();

        if (propBlocker != null)
        {
            propBlocker.Configure(footprint);
        }

        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        if (collider != null)
        {
            collider.isTrigger = false;
            collider.offset = ResolveColliderOffsetInLocalSpace();
            collider.size = ResolveColliderSizeInLocalSpace();
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            renderer.color = visualColor;
            renderer.sortingOrder = PreviewSortingOrder;
            renderer.enabled = !Application.isPlaying;
        }

        if (allowHierarchyMutation)
        {
            ApplyBlockingState(collider);
        }

        SyncRuntimeVisuals(renderer);
    }

    private void SyncFootprintFromPlacedScale()
    {
        Vector3 placedScale = transform.localScale;
        footprint = new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(placedScale.x))),
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(placedScale.y))));
    }

    private float ResolveDepthScale()
    {
        return Mathf.Approximately(transform.localScale.z, 0f) ? 1f : transform.localScale.z;
    }

    private Vector2 ResolveColliderSizeInLocalSpace()
    {
        Vector2 worldSize = ResolveColliderWorldSize();
        Vector2 scale = ResolveAbsoluteLossyScale2D();
        return new Vector2(
            Mathf.Max(MinimumColliderAxis, worldSize.x / scale.x),
            Mathf.Max(MinimumColliderAxis, worldSize.y / scale.y));
    }

    private Vector2 ResolveColliderOffsetInLocalSpace()
    {
        Vector2 scale = ResolveAbsoluteLossyScale2D();
        return new Vector2(
            colliderLocalOffset.x / scale.x,
            colliderLocalOffset.y / scale.y);
    }

    private Vector2 ResolveColliderWorldSize()
    {
        return new Vector2(
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.x * Mathf.Max(1, footprint.x)),
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.y * Mathf.Max(1, footprint.y)));
    }

    private Vector2 ResolveAbsoluteLossyScale2D()
    {
        Vector3 lossyScale = transform.lossyScale;
        return new Vector2(
            Mathf.Max(MinimumColliderAxis, Mathf.Abs(lossyScale.x)),
            Mathf.Max(MinimumColliderAxis, Mathf.Abs(lossyScale.y)));
    }

    private void ApplyBlockingState(BoxCollider2D collider)
    {
        gameObject.layer = GameLayers.WallIndex;

        if (collider != null)
        {
            RuntimeShadowCaster2DConfigurator.TryConfigureFromCollider(gameObject, collider, out _);
        }
    }

    private void SyncRuntimeVisuals(SpriteRenderer sourceRenderer)
    {
        if (!Application.isPlaying || runtimeVisualStyle == RuntimeVisualStyle.None)
        {
            DestroyGeneratedVisualRoot();
            return;
        }

        switch (runtimeVisualStyle)
        {
            case RuntimeVisualStyle.DividerScreen:
                BuildDividerScreenVisual(sourceRenderer);
                break;
        }
    }

    private void BuildDividerScreenVisual(SpriteRenderer sourceRenderer)
    {
        Transform visualRoot = GetOrCreateVisualRoot();
        Vector2 scaledColliderSize = new(
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.x * Mathf.Max(1, footprint.x)),
            Mathf.Max(MinimumColliderAxis, colliderLocalSize.y * Mathf.Max(1, footprint.y)));
        bool horizontal = scaledColliderSize.x >= scaledColliderSize.y;
        float frameMajor = runtimeMajorAxisFill;
        float frameMinor = runtimeMinorAxisFill;
        float capThickness = Mathf.Min(runtimeCapThickness, frameMajor * 0.22f);
        float bandThickness = Mathf.Min(runtimeBandThickness, frameMajor * 0.18f);

        Vector2 frameSize = horizontal
            ? new Vector2(frameMajor, frameMinor)
            : new Vector2(frameMinor, frameMajor);
        Vector2 panelSize = horizontal
            ? new Vector2(frameSize.x * 0.86f, frameSize.y * 0.72f)
            : new Vector2(frameSize.x * 0.72f, frameSize.y * 0.86f);
        Vector2 capSize = horizontal
            ? new Vector2(capThickness, frameSize.y * 1.08f)
            : new Vector2(frameSize.x * 1.08f, capThickness);
        Vector2 bandSize = horizontal
            ? new Vector2(bandThickness, panelSize.y * 0.92f)
            : new Vector2(panelSize.x * 0.92f, bandThickness);
        float capOffset = horizontal
            ? (frameSize.x - capSize.x) * 0.5f
            : (frameSize.y - capSize.y) * 0.5f;
        float bandOffset = horizontal
            ? panelSize.x * 0.24f
            : panelSize.y * 0.24f;

        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "Frame"),
            sourceRenderer,
            runtimeFrameColor,
            runtimeVisualSortingOrder,
            Vector3.zero,
            frameSize);
        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "Panel"),
            sourceRenderer,
            runtimePanelColor,
            runtimeVisualSortingOrder + 1,
            Vector3.zero,
            panelSize);

        Vector3 capAPosition = horizontal
            ? new Vector3(-capOffset, 0f, 0f)
            : new Vector3(0f, -capOffset, 0f);
        Vector3 capBPosition = horizontal
            ? new Vector3(capOffset, 0f, 0f)
            : new Vector3(0f, capOffset, 0f);
        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "CapA"),
            sourceRenderer,
            runtimeFrameColor,
            runtimeVisualSortingOrder + 2,
            capAPosition,
            capSize);
        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "CapB"),
            sourceRenderer,
            runtimeFrameColor,
            runtimeVisualSortingOrder + 2,
            capBPosition,
            capSize);

        Vector3 bandAPosition = horizontal
            ? new Vector3(-bandOffset, 0f, 0f)
            : new Vector3(0f, -bandOffset, 0f);
        Vector3 bandBPosition = horizontal
            ? new Vector3(bandOffset, 0f, 0f)
            : new Vector3(0f, bandOffset, 0f);
        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "BandA"),
            sourceRenderer,
            runtimeHighlightColor,
            runtimeVisualSortingOrder + 3,
            bandAPosition,
            bandSize);
        ConfigureVisualPart(
            GetOrCreateVisualPart(visualRoot, "BandB"),
            sourceRenderer,
            runtimeHighlightColor,
            runtimeVisualSortingOrder + 3,
            bandBPosition,
            bandSize);
    }

    private Transform GetOrCreateVisualRoot()
    {
        Transform visualRoot = transform.Find(GeneratedVisualRootName);

        if (visualRoot != null)
        {
            return visualRoot;
        }

        GameObject visualRootObject = new(GeneratedVisualRootName);
        visualRoot = visualRootObject.transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
        return visualRoot;
    }

    private Transform GetOrCreateVisualPart(Transform visualRoot, string partName)
    {
        Transform part = visualRoot.Find(partName);

        if (part != null)
        {
            return part;
        }

        GameObject partObject = new(partName);
        part = partObject.transform;
        part.SetParent(visualRoot, false);
        part.localPosition = Vector3.zero;
        part.localRotation = Quaternion.identity;
        part.localScale = Vector3.one;
        partObject.AddComponent<SpriteRenderer>();
        return part;
    }

    private void ConfigureVisualPart(
        Transform part,
        SpriteRenderer sourceRenderer,
        Color color,
        int sortingOrder,
        Vector3 localPosition,
        Vector2 localScale)
    {
        if (part == null)
        {
            return;
        }

        part.localPosition = localPosition;
        part.localRotation = Quaternion.identity;
        part.localScale = new Vector3(localScale.x, localScale.y, 1f);

        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();

        if (renderer == null)
        {
            renderer = part.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = GetGeneratedSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.maskInteraction = SpriteMaskInteraction.None;

        if (sourceRenderer != null)
        {
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        }
    }

    private static Sprite GetGeneratedSprite()
    {
        if (generatedSprite != null)
        {
            return generatedSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        generatedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
        return generatedSprite;
    }

    private void DestroyGeneratedVisualRoot()
    {
        Transform visualRoot = transform.Find(GeneratedVisualRootName);

        if (visualRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(visualRoot.gameObject);
        }
        else
        {
            DestroyImmediate(visualRoot.gameObject);
        }
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (Application.isPlaying)
        {
            return;
        }

        EditorApplication.delayCall -= RefreshEditorState;
        EditorApplication.delayCall += RefreshEditorState;
    }

    private void RefreshEditorState()
    {
        EditorApplication.delayCall -= RefreshEditorState;

        if (this == null || gameObject == null)
        {
            return;
        }

        ApplyBlockingState(GetComponent<BoxCollider2D>());
        SyncRuntimeVisuals(GetComponent<SpriteRenderer>());
    }
#endif
}
