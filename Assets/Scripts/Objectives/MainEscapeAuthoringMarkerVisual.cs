using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MainEscapeAuthoringMarkerVisual : MonoBehaviour
{
    private const int RuntimeObjectiveSortingOrder = 140;
    private const string VisualChildName = "Visual";
    private const float VisualAlignmentEpsilon = 0.0001f;

    [SerializeField] private bool hideWhilePlaying = true;
    [SerializeField] private bool showInPlayForDiscovery;
    [SerializeField] private SpriteRenderer[] spriteRenderers = Array.Empty<SpriteRenderer>();
    private bool renderersCached;

    [ContextMenu("Snap Visual Children To Marker Origin")]
    private void SnapVisualChildrenToMarkerOrigin()
    {
        TryAlignVisualChildrenToMarkerOrigin();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        ApplyVisibilityDeferredIfNeeded();
    }

    private void Reset()
    {
        CacheRenderers(force: true);
        ApplyVisibilityDeferredIfNeeded();
    }

    private void Awake()
    {
        CacheRenderersIfNeeded();
        ApplyVisibilityDeferredIfNeeded();
    }

    private void OnEnable()
    {
        CacheRenderersIfNeeded();
        ApplyVisibilityDeferredIfNeeded();
    }

    private void Start()
    {
        CacheRenderersIfNeeded();
        EnsureRuntimeItemDiscoveryIfNeeded();
        ApplyVisibilityDeferredIfNeeded();
    }

    private void OnValidate()
    {
        CacheRenderers(force: true);
        ApplyVisibilityDeferredIfNeeded();
    }

    private void OnTransformChildrenChanged()
    {
        CacheRenderers(force: true);
        ApplyVisibilityDeferredIfNeeded();
    }

    private void ApplyVisibilityDeferredIfNeeded()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= ApplyVisibilityInEditor;
            EditorApplication.delayCall += ApplyVisibilityInEditor;
            return;
        }
#endif

        ApplyVisibility();
    }

    private void CacheRenderersIfNeeded()
    {
        if (renderersCached && spriteRenderers != null)
        {
            return;
        }

        CacheRenderers(force: false);
    }

    private void CacheRenderers(bool force)
    {
        if (!force && renderersCached && spriteRenderers != null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            TryAlignVisualChildrenToMarkerOrigin();
        }

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        renderersCached = true;
    }

    private bool TryAlignVisualChildrenToMarkerOrigin()
    {
        bool changed = false;

        for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            Transform child = transform.GetChild(childIndex);

            if (child == null || !string.Equals(child.name, VisualChildName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            changed |= TryAlignVisualChildToMarkerOrigin(child);
        }

        return changed;
    }

    private bool TryAlignVisualChildToMarkerOrigin(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return false;
        }

        SpriteRenderer[] childRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        Bounds combinedBounds = default;
        bool hasBounds = false;

        for (int index = 0; index < childRenderers.Length; index++)
        {
            SpriteRenderer childRenderer = childRenderers[index];

            if (childRenderer == null || childRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = childRenderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(childRenderer.bounds);
        }

        if (!hasBounds)
        {
            return false;
        }

        Vector3 visualCenterInMarkerSpace = transform.InverseTransformPoint(combinedBounds.center);
        Vector3 expectedLocalPosition = new(
            visualRoot.localPosition.x - visualCenterInMarkerSpace.x,
            visualRoot.localPosition.y - visualCenterInMarkerSpace.y,
            visualRoot.localPosition.z);
        bool changed = false;

        if ((visualRoot.localPosition - expectedLocalPosition).sqrMagnitude > VisualAlignmentEpsilon * VisualAlignmentEpsilon)
        {
            visualRoot.localPosition = expectedLocalPosition;
            changed = true;
        }

        if (Quaternion.Angle(visualRoot.localRotation, Quaternion.identity) > VisualAlignmentEpsilon)
        {
            visualRoot.localRotation = Quaternion.identity;
            changed = true;
        }

        return changed;
    }

    private void ApplyVisibility()
    {
        bool visible = !hideWhilePlaying || !Application.isPlaying || ShouldUseItemDiscoveryInPlay();

        if (spriteRenderers == null)
        {
            return;
        }

        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer renderer = spriteRenderers[index];

            if (renderer != null)
            {
                ApplyMarkerGlyph(renderer);
                renderer.enabled = visible;
            }
        }
    }

    private void ApplyMarkerGlyph(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        string markerName = gameObject.name;
        bool isKeyMarker = markerName.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0;

        if (markerName.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(renderer);
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
            return;
        }

        if (isKeyMarker)
        {
            MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(renderer);
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, RuntimeObjectiveSortingOrder);
            return;
        }

        if (markerName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0
            || markerName.IndexOf("Bottle", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(renderer);
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
            return;
        }

        if (markerName.IndexOf("Tool", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(renderer);
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
            return;
        }

        if (markerName.IndexOf("Transition", StringComparison.OrdinalIgnoreCase) >= 0
            || markerName.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0
            || markerName.IndexOf("Escape", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(renderer);
            MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
        }
    }

    private void EnsureRuntimeItemDiscoveryIfNeeded()
    {
        if (!ShouldUseItemDiscoveryInPlay() || spriteRenderers == null)
        {
            return;
        }

        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer renderer = spriteRenderers[index];

            if (renderer == null)
            {
                continue;
            }

            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, RuntimeObjectiveSortingOrder);
            PickupFlashlightDiscoveryController discoveryController = renderer.GetComponent<PickupFlashlightDiscoveryController>();

            if (discoveryController == null)
            {
                discoveryController = renderer.gameObject.AddComponent<PickupFlashlightDiscoveryController>();
            }

            discoveryController.Initialize(renderer, renderer.color);
        }
    }

    private bool ShouldUseItemDiscoveryInPlay()
    {
        return showInPlayForDiscovery
            && Application.isPlaying
            && gameObject.scene.IsValid()
            && RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene)
            && gameObject.name.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0;
    }

#if UNITY_EDITOR
    private void ApplyVisibilityInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheRenderersIfNeeded();
        ApplyVisibility();
    }
#endif
}
