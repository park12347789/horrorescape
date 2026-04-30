using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FloorEscapeGoalPickup : PlayerInteractable2D
{
    private const int SortingOrder = 140;

    [FormerlySerializedAs("controller")]
    [SerializeField] private MonoBehaviour rebuildControllerSource;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private string displayName = "Floor Tool";
    [SerializeField] private Color activeColor = new(1f, 0.8f, 0.2f, 1f);

    private IFloorEscapeInteractionController interactionController;
    private bool hasRenderedState;
    private bool renderedVisible;
    private Color renderedColor;

    private void Awake()
    {
        CacheController();
        CacheSpriteRenderer();
        RefreshVisual();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void Reset()
    {
        CacheController();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void OnValidate()
    {
        CacheController();
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisualDeferredIfNeeded();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(IFloorEscapeInteractionController owner, Vector3 worldPosition, string displayName, Color color)
    {
        BindController(owner);
        ConfigureInternal(worldPosition, displayName, color);
    }

    public void ConfigureAuthored(IFloorEscapeInteractionController owner, string configuredDisplayName, Color color)
    {
        BindController(owner);
        CacheController();
        displayName = configuredDisplayName;
        activeColor = color;
        CacheSpriteRenderer();
        InvalidateRenderedState();
        RefreshVisual();
        RefreshState();
    }

    private void ConfigureInternal(Vector3 worldPosition, string displayName, Color color)
    {
        CacheController();
        this.displayName = displayName;
        activeColor = color;
        CacheSpriteRenderer();
        gameObject.name = $"Pickup_{displayName.Replace(' ', '_')}";
        transform.position = worldPosition;
        transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        InvalidateRenderedState();
        RefreshVisual();
        gameObject.SetActive(true);
        RefreshState();
    }

    public void RefreshState()
    {
        CacheController();
        CacheSpriteRenderer();
        bool visible = interactionController != null && interactionController.IsPickupVisible;

        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible || spriteRenderer == null)
        {
            StoreRenderedState(visible, default);
            return;
        }

        Color targetColor = new(activeColor.r, activeColor.g, activeColor.b, 1f);

        if (hasRenderedState && renderedVisible && renderedColor == targetColor)
        {
            return;
        }

        ApplyVisualState(targetColor);
        StoreRenderedState(true, targetColor);
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return base.CanInteractAtDistance(playerController, interactionDistance)
            && interactionController != null
            && interactionController.IsPickupVisible;
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        return "E Recover Floor Tool";
    }

    public override void Interact(WasdPlayerController playerController)
    {
        interactionController?.TryRecoverCurrentTool();
        RefreshState();
    }

    private void CacheController()
    {
        interactionController = rebuildControllerSource as IFloorEscapeInteractionController;
    }

    private void BindController(IFloorEscapeInteractionController owner)
    {
        rebuildControllerSource = owner as MonoBehaviour;
    }

    private void RefreshVisual()
    {
        CacheSpriteRenderer();

        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(spriteRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        ApplyVisualState(activeColor);
    }

    private void ApplyVisualState(Color targetColor)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(spriteRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);

        if (spriteRenderer.color != targetColor)
        {
            spriteRenderer.color = targetColor;
        }

        ApplySceneMaterial(spriteRenderer);
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, SortingOrder);
    }

    private void CacheSpriteRenderer()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
    }

    private void RefreshVisualDeferredIfNeeded()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= RefreshVisualInEditor;
            EditorApplication.delayCall += RefreshVisualInEditor;
            return;
        }
#endif

        RefreshVisual();
    }

    private static void ApplySceneMaterial(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(renderer);
    }

    private void StoreRenderedState(bool visible, Color color)
    {
        hasRenderedState = true;
        renderedVisible = visible;
        renderedColor = color;
    }

    private void InvalidateRenderedState()
    {
        hasRenderedState = false;
    }

#if UNITY_EDITOR
    private void RefreshVisualInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheSpriteRenderer();
        RefreshVisual();
    }
#endif
}
