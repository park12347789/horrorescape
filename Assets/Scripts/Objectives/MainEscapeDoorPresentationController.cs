using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeDoorPresentationController : MonoBehaviour
{
    private const string DefaultVisualName = "DoorPresentation";

    [SerializeField, HideInInspector] private SpriteRenderer managedRenderer;
    [SerializeField] private string visualName = DefaultVisualName;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private bool isOpen;
    [SerializeField] private bool visible = true;

    public SpriteRenderer Renderer
    {
        get
        {
            SpriteRenderer targetRenderer = EnsureRendererExists();
            ApplyPresentationState(targetRenderer);
            return targetRenderer;
        }
    }

    public void Configure(string configuredVisualName, Sprite configuredClosedSprite, Sprite configuredOpenSprite)
    {
        visualName = ResolveVisualName(configuredVisualName);
        closedSprite = configuredClosedSprite;
        openSprite = configuredOpenSprite;
        ApplyPresentationState();
    }

    public void SetOpen(bool configuredIsOpen)
    {
        isOpen = configuredIsOpen;
        ApplyPresentationState();
    }

    public void SetVisible(bool configuredVisible)
    {
        visible = configuredVisible;
        ApplyPresentationState();
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            ApplyPresentationState();
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            ApplyPresentationState();
        }
    }

    private void OnTransformChildrenChanged()
    {
        if (!IsManagedRendererValid(managedRenderer))
        {
            managedRenderer = null;
        }
    }

    private void ApplyPresentationState()
    {
        ApplyPresentationState(EnsureRendererExists());
    }

    private void ApplyPresentationState(SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.gameObject.name = ResolveVisualName(visualName);
        targetRenderer.gameObject.layer = gameObject.layer;
        targetRenderer.sprite = ResolveCurrentSprite();
        targetRenderer.enabled = visible;
    }

    private Sprite ResolveCurrentSprite()
    {
        return isOpen ? openSprite ?? closedSprite : closedSprite ?? openSprite;
    }

    private SpriteRenderer EnsureRendererExists()
    {
        visualName = ResolveVisualName(visualName);

        if (IsManagedRendererValid(managedRenderer))
        {
            return managedRenderer;
        }

        if (TryGetManagedChild(out Transform managedChild))
        {
            managedRenderer = managedChild.GetComponent<SpriteRenderer>();

            if (managedRenderer == null)
            {
                managedRenderer = managedChild.gameObject.AddComponent<SpriteRenderer>();
                ApplyCreatedRendererDefaults(managedRenderer);
            }

            return managedRenderer;
        }

        GameObject childObject = new(visualName);
        childObject.layer = gameObject.layer;
        childObject.transform.SetParent(transform, false);

        managedRenderer = childObject.AddComponent<SpriteRenderer>();
        ApplyCreatedRendererDefaults(managedRenderer);
        return managedRenderer;
    }

    // Recover by name first so repeated state changes never duplicate the managed child.
    private bool TryGetManagedChild(out Transform managedChild)
    {
        string expectedName = ResolveVisualName(visualName);

        for (int index = 0; index < transform.childCount; index++)
        {
            Transform child = transform.GetChild(index);

            if (child == null || !string.Equals(child.name, expectedName, StringComparison.Ordinal))
            {
                continue;
            }

            managedChild = child;
            return true;
        }

        managedChild = null;
        return false;
    }

    private void ApplyCreatedRendererDefaults(SpriteRenderer targetRenderer)
    {
        if (targetRenderer == null)
        {
            return;
        }

        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();

        if (sourceRenderer == null)
        {
            return;
        }

        targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        targetRenderer.sortingOrder = sourceRenderer.sortingOrder;
        targetRenderer.maskInteraction = sourceRenderer.maskInteraction;
        targetRenderer.flipX = sourceRenderer.flipX;
        targetRenderer.flipY = sourceRenderer.flipY;
        targetRenderer.drawMode = SpriteDrawMode.Simple;
        targetRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
        targetRenderer.color = sourceRenderer.color;
    }

    private bool IsManagedRendererValid(SpriteRenderer renderer)
    {
        return renderer != null && renderer.transform.parent == transform;
    }

    private static string ResolveVisualName(string configuredVisualName)
    {
        return string.IsNullOrWhiteSpace(configuredVisualName)
            ? DefaultVisualName
            : configuredVisualName.Trim();
    }
}
