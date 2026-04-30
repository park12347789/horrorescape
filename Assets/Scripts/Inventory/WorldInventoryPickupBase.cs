using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class WorldInventoryPickupBase : PlayerInteractable2D
{
    private const int MainSceneSortingOrder = 140;

    [SerializeField] private string itemId = PrototypeItemCatalog.GlassBottleItemId;
    [SerializeField] private string inventoryDisplayName = "Item";
    [SerializeField] private int quantity = 1;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color collectedColor = new(1f, 1f, 1f, 0.2f);
    [SerializeField] private PickupFlashlightDiscoveryController discoveryController;
    [SerializeField, Min(0f)] private float pickupReachPadding = 0.14f;
    [SerializeField] private bool suppressRuntimeManagedPickupReplacement;

    public string ItemId => itemId;
    public string InventoryDisplayName => inventoryDisplayName;
    public int Quantity => quantity;
    public bool SuppressRuntimeManagedPickupReplacement => suppressRuntimeManagedPickupReplacement;
    protected Color BaseColor => baseColor;

    protected virtual string InteractionVerb => "Pick Up";
    protected virtual bool UseDiscoveryVisibility => IsAuthoredMainScene();
    protected virtual bool ApplyMainSceneScale => false;
    protected virtual Vector3 MainSceneScale => Vector3.one;

    protected virtual void Awake()
    {
        CacheReferences();
        RefreshVisual();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheReferences();

        if (Application.isPlaying)
        {
            RefreshVisual();
        }
    }

    protected virtual void Start()
    {
        EnsureDiscoveryVisibilityController();
    }

    protected virtual void OnValidate()
    {
        CacheReferences();

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

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        return $"E {InteractionVerb} {inventoryDisplayName}";
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 interactionSurfacePoint = ResolveInteractionSurfacePoint(playerPosition);
        float surfaceDistance = Vector2.Distance(playerPosition, interactionSurfacePoint);
        return Mathf.Max(0f, surfaceDistance - pickupReachPadding);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return playerController != null
            ? ResolveInteractionSurfacePoint(playerController.transform.position)
            : InteractionPoint;
    }

    public override void Interact(WasdPlayerController playerController)
    {
        PlayerInventory inventory = playerController != null ? playerController.GetComponent<PlayerInventory>() : null;

        if (!TryCollect(inventory))
        {
            return;
        }

        MarkCollected();
        PrototypeAudioManager.TryPlayPickup();
        gameObject.SetActive(false);
    }

    protected void ConfigurePickupDefinition(
        string configuredItemId,
        string displayName,
        int configuredQuantity,
        Color? configuredBaseColor = null,
        Vector3? worldPosition = null,
        bool activate = false)
    {
        string previousItemId = itemId;
        itemId = string.IsNullOrWhiteSpace(configuredItemId)
            ? itemId
            : configuredItemId;
        inventoryDisplayName = PrototypeItemCatalog.GetDisplayName(itemId, displayName);
        quantity = Mathf.Max(1, configuredQuantity);

        if (configuredBaseColor.HasValue)
        {
            baseColor = configuredBaseColor.Value;
        }

        if (worldPosition.HasValue)
        {
            transform.position = worldPosition.Value;
        }

        PrototypeItemUiIconResolver.Invalidate(previousItemId);
        PrototypeItemUiIconResolver.Invalidate(itemId);
        CacheReferences();
        RefreshVisual();
        EnsureDiscoveryVisibilityController();
        discoveryController?.ResetDiscovery();

        if (activate)
        {
            gameObject.SetActive(true);
        }
    }

    protected virtual bool TryCollect(PlayerInventory inventory)
    {
        return inventory != null && inventory.AddItem(itemId, inventoryDisplayName, quantity);
    }

    protected virtual SpriteRenderer ResolveSpriteRenderer()
    {
        return GetComponent<SpriteRenderer>();
    }

    protected void CacheReferences()
    {
        spriteRenderer ??= ResolveSpriteRenderer();

        if (spriteRenderer != null)
        {
            discoveryController ??= spriteRenderer.GetComponent<PickupFlashlightDiscoveryController>();
        }

        discoveryController ??= GetComponent<PickupFlashlightDiscoveryController>();
    }

    protected void RefreshVisual()
    {
        CacheReferences();

        if (spriteRenderer == null)
        {
            return;
        }

        MainEscapeRuntimeVisualDefaults.EnsurePickupSprite(spriteRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);
        spriteRenderer.color = baseColor;
        PrototypeItemUiIconResolver.Invalidate(itemId);
        ApplySceneVisibilityOverrides();
        SyncDiscoveryVisibility();
    }

    protected void MarkCollected()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = collectedColor;
        }
    }

    private Vector2 ResolveInteractionSurfacePoint(Vector2 playerPosition)
    {
        CacheReferences();

        if (spriteRenderer != null && spriteRenderer.bounds.size.sqrMagnitude > 0.0001f)
        {
            return spriteRenderer.bounds.ClosestPoint(playerPosition);
        }

        Collider2D pickupCollider = GetComponent<Collider2D>();
        if (pickupCollider != null && pickupCollider.enabled)
        {
            return pickupCollider.ClosestPoint(playerPosition);
        }

        return InteractionPoint;
    }

    private void ApplySceneVisibilityOverrides()
    {
        if (!IsAuthoredMainScene() || spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.enabled = true;
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, MainSceneSortingOrder);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(spriteRenderer);

        if (ApplyMainSceneScale)
        {
            transform.localScale = MainSceneScale;
        }
    }

    private void SyncDiscoveryVisibility()
    {
        if (!Application.isPlaying || !UseDiscoveryVisibility || spriteRenderer == null)
        {
            return;
        }

        discoveryController ??= spriteRenderer.GetComponent<PickupFlashlightDiscoveryController>();
        discoveryController?.Initialize(spriteRenderer, baseColor);
    }

    private void EnsureDiscoveryVisibilityController()
    {
        if (!Application.isPlaying || !UseDiscoveryVisibility || spriteRenderer == null)
        {
            return;
        }

        discoveryController ??= spriteRenderer.GetComponent<PickupFlashlightDiscoveryController>();

        if (discoveryController == null)
        {
            discoveryController = spriteRenderer.gameObject.AddComponent<PickupFlashlightDiscoveryController>();
        }

        discoveryController.Initialize(spriteRenderer, baseColor);
    }

    private bool IsAuthoredMainScene()
    {
        return gameObject.scene.IsValid()
            && RSceneRouteMembershipUtility.IsManagedGameplayOrAuthoredScene(gameObject.scene);
    }

#if UNITY_EDITOR
    private void RefreshVisualInEditor()
    {
        if (this == null)
        {
            return;
        }

        CacheReferences();
        RefreshVisual();
    }
#endif
}
